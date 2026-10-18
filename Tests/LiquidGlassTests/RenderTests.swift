import ImageIO
@testable import LiquidGlass
import SwiftUI
import XCTest

/// End to end: real `GlassScene`s with glass, rendered by SwiftUI through the
/// Metal layer effect, read back and compared with the CPU model. This checks
/// what the vector tests cannot: that the backdrop copy lines up with the
/// visible backdrop (left to right and right to left, at 1x and 2x, under an
/// ancestor's clip), that colors reach the shader as unpremultiplied sRGB,
/// that the overlay composites like the spec's `rim + glass(1 − rim.a)` over
/// the backdrop, the Regular frost, tint, the fallbacks, and the layout of
/// bars, menus and blurs in either direction. Model comparisons cover every
/// pixel of a surface's bounds and two pixels around them, the antialiased
/// rim included.
@MainActor
final class RenderTests: XCTestCase {
    private let size = CGSize(width: 400, height: 200)
    private let surface = CGSize(width: 200, height: 64)
    /// 8-bit readback of an 8-bit backdrop: about one step in and one out.
    private let tolerance = 3.0 / 255.0

    private var library: ShaderLibrary { ShaderLibrary.bundle(.module) }
    private var renders = 0

    /// The linear test backdrop of the glassPixel vectors, centered in the scene.
    private var gradient: some View {
        Rectangle().fill(.white).colorEffect(library.lgTestBackdrop(.float2(CGPoint(x: size.width / 2, y: size.height / 2))))
    }

    private func wave(_ wavelength: Double) -> some View {
        Rectangle().fill(.white).colorEffect(library.lgTestWave(.float(wavelength)))
    }

    private func centered<Content: View>(_ content: Content) -> some View {
        content.frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    private func capsule(tint: Color? = nil) -> some View {
        GlassSurface(radius: .capsule, tint: tint) {
            Color.clear.frame(width: surface.width, height: surface.height)
        }
    }

    // MARK: Clear

    /// The model's color at a pixel center `(px, py)` (in points) of a
    /// capsule centered in the scene, over the gradient. `refraction` `nil`
    /// is the default for the capsule's size (48 for 200 × 64).
    private func expected(px: Double, py: Double, material: GlassMaterial = .clear, dark: Bool,
                          pixel: Double = 1, tint: SIMD3<Double>? = nil, refraction: Double? = nil) -> SIMD4<Double> {
        let x = px - Double(size.width) / 2, y = py - Double(size.height) / 2
        let width = Double(surface.width), height = Double(surface.height)
        return GlassOptics.glassPixel(
            x: x, y: y, width: width, height: height, radius: .capsule, material: material, dark: dark,
            refraction: refraction ?? GlassOptics.defaultRefraction(width: width, height: height), tint: tint, pixel: pixel,
            sample: { dx, dy in vectorBackdrop(x + dx, y + dy) }, base: vectorBackdrop(x, y)
        )
    }

    /// Pixel centers: inside the face, in the bevel, on the rim and outside.
    private let probes: [(Int, Int)] = [(200, 100), (130, 100), (285, 100), (200, 72), (200, 128), (110, 100), (60, 30)]

    /// The capsule's bounds in the scene, in points.
    private var capsuleRect: CGRect {
        CGRect(x: (size.width - surface.width) / 2, y: (size.height - surface.height) / 2, width: surface.width, height: surface.height)
    }

    private func checkClear(appearance: GlassAppearance, scale: CGFloat = 1, tint: Color? = nil, srgbTint: SIMD3<Double>? = nil) throws {
        let image = try render(GlassScene(material: .clear, appearance: appearance) {
            GlassContent { gradient }
            centered(capsule(tint: tint))
        }, scale: scale)
        let s = Double(scale)
        assertMatchesModel(image, in: capsuleRect, "clear \(appearance) @\(Int(scale))x") { px, py in
            expected(px: (Double(px) + 0.5) / s, py: (Double(py) + 0.5) / s, dark: appearance == .dark, pixel: 1 / s, tint: srgbTint)
        }
        // Far outside, the backdrop alone.
        assertClose(image.pixel(Int(60 * s), Int(30 * s)).array, vectorBackdrop(60.5 - 200, 30.5 - 100).array, accuracy: tolerance, "outside")
    }

    func testClearLight() throws { try checkClear(appearance: .light) }
    func testClearDark() throws { try checkClear(appearance: .dark) }
    func testClearRetina() throws { try checkClear(appearance: .light, scale: 2) }

    func testTint() throws {
        let tint = SIMD3<Double>(0.2, 0.5, 0.9)
        try checkClear(appearance: .light, tint: Color(.sRGB, red: tint.x, green: tint.y, blue: tint.z), srgbTint: tint)
    }

    // MARK: Refraction

    /// Glass that sets no refraction travels by its size (the 200 × 64
    /// capsule 48 points, not 60, `spec/OPTICS.md` §2); a scene's or an
    /// environment's refraction applies at every size; a surface's own wins.
    func testRefractionPrecedence() throws {
        let cases: [(String, scene: Double?, environment: Double?, surface: Double?, expected: Double)] = [
            ("unset", nil, nil, nil, 48),
            ("scene's", 60, nil, nil, 60),
            ("environment's", 60, 30, nil, 30),
            ("surface's", 60, 30, 12, 12),
        ]
        for (name, scene, environment, own, refraction) in cases {
            let glass = GlassSurface(radius: .capsule, refraction: own) { Color.clear.frame(width: surface.width, height: surface.height) }
            let image = try render(GlassScene(material: .clear, appearance: .light, refraction: scene) {
                GlassContent { gradient }
                centered(environment.map { AnyView(glass.glassRefraction($0)) } ?? AnyView(glass))
            })
            assertMatchesModel(image, in: capsuleRect, "\(name) refraction") { px, py in
                expected(px: Double(px) + 0.5, py: Double(py) + 0.5, dark: false, refraction: refraction)
            }
        }
        // Over this gradient 48 and 60 points of travel differ by up to
        // 5.5/255 near the rim, more than the 3/255 tolerance, so the checks
        // tell even those two apart.
        var largest = 0.0
        for py in Int(capsuleRect.minY)..<Int(capsuleRect.maxY) {
            for px in Int(capsuleRect.minX)..<Int(capsuleRect.maxX) {
                let x = Double(px) + 0.5, y = Double(py) + 0.5
                let d = expected(px: x, py: y, dark: false, refraction: 48) - expected(px: x, py: y, dark: false, refraction: 60)
                largest = max(largest, d.array.map(abs).max() ?? 0)
            }
        }
        XCTAssertGreaterThan(largest, 1.5 * tolerance)
    }

    // MARK: Regular

    /// Regular glass over a sine: the frost and the fill are Gaussian blurs of
    /// it, whose responses are known exactly, so the shader's blur, its fill
    /// kernel and the frost blend can be checked against `frostColor`.
    private func checkRegular(appearance: GlassAppearance) throws {
        let wavelength = 120.0
        let dark = appearance == .dark
        let image = try render(GlassScene(material: .regular, appearance: appearance) {
            GlassContent { wave(wavelength) }
            centered(capsule())
        }, scale: 2)
        let k = 2 * Double.pi / wavelength
        func blurred(_ x: Double, sigma: Double) -> SIMD3<Double> {
            SIMD3(0.5 + 0.3 * exp(-0.5 * k * k * sigma * sigma) * sin(k * x), 0.45, 0.6)
        }
        let frostSigma = GlassOptics.frostSigma, fillSigma = GlassOptics.fillSigma(dark: dark)
        // Away from the capsule's ends, where the fill kernel would reach past
        // the layer, and on the face, the bevel and the rim.
        for (px, py) in [(310, 200), (400, 200), (480, 200), (360, 150), (440, 250), (400, 140), (400, 260)] {
            let x = (Double(px) + 0.5) / 2, y = (Double(py) + 0.5) / 2
            let cx = x - Double(size.width) / 2, cy = y - Double(size.height) / 2
            let width = Double(surface.width), height = Double(surface.height)
            let model = GlassOptics.glassPixel(
                x: cx, y: cy, width: width, height: height, radius: .capsule, material: .regular, dark: dark,
                refraction: GlassOptics.defaultRefraction(width: width, height: height), pixel: 0.5,
                sample: { dx, _ in
                    let c = GlassOptics.frostColor(frost: blurred(x + dx, sigma: frostSigma), fill: blurred(x + dx, sigma: fillSigma), dark: dark)
                    return SIMD4(c.x, c.y, c.z, 1)
                },
                base: SIMD4(0.5 + 0.3 * sin(k * x), 0.45, 0.6, 1)
            )
            assertClose(image.pixel(px, py).array, model.array, accuracy: tolerance, "regular \(appearance) pixel (\(px), \(py))")
        }
    }

    func testRegularLight() throws { try checkRegular(appearance: .light) }
    func testRegularDark() throws { try checkRegular(appearance: .dark) }

    /// Text under Regular glass frosts into a soft blur, never into
    /// hard-edged bars along each line's frame.
    func testRegularFrostsTextSmoothly() throws {
        let image = try render(GlassScene(material: .regular, appearance: .dark) {
            GlassContent {
                VStack(alignment: .leading, spacing: 6) {
                    Text("MATERIALS").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                    Text("A field guide").font(.caption).foregroundStyle(.white.opacity(0.6))
                    Text("Frosted glass").font(.title.bold()).foregroundStyle(.primary)
                    Text("Regular glass blurs what is behind it.").font(.body).foregroundStyle(.secondary)
                }
                .padding(.top, 50)
                .padding(.leading, 60)
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                .background(Color(white: 0.06))
                .environment(\.colorScheme, .dark)
            }
            centered(GlassSurface(radius: 28) { Color.clear.frame(width: 300, height: 140) })
        }, scale: 2)
        // The face: 24 points inside the outline, past the bevel.
        var jump = 0.0, at = (0, 0)
        for py in stride(from: 2 * (30 + 24), to: 2 * (170 - 24), by: 1) {
            for px in stride(from: 2 * (50 + 24), to: 2 * (350 - 24) - 1, by: 1) {
                let a = image.pixel(px, py), b = image.pixel(px + 1, py), c = image.pixel(px, py + 1)
                let d = max(abs(a.x - b.x), abs(a.x - c.x))
                if d > jump { jump = d; at = (px, py) }
            }
        }
        XCTAssertLessThan(jump, 0.02, "largest step between neighboring pixels on frosted glass, at \(at)")
    }

    // MARK: Right to left

    /// An off-center surface right to left refracts the backdrop under its
    /// own position: the copy's alignment doesn't mirror.
    private func checkOffCenter(_ direction: LayoutDirection, scale: CGFloat = 1) throws {
        let face = CGSize(width: 140, height: 100)
        func scene(glass: Bool) -> some View {
            GlassScene(material: .clear, appearance: .light) {
                GlassContent { gradient }
                HStack(spacing: 0) {
                    if glass {
                        GlassSurface(radius: 24) { Color.clear.frame(width: face.width, height: face.height) }
                    } else {
                        Color.clear.frame(width: face.width, height: face.height)
                    }
                    Spacer(minLength: 0)
                }
                .padding(.leading, 20)
                .frame(maxHeight: .infinity)
            }
            .environment(\.layoutDirection, direction)
        }
        let plain = try render(scene(glass: false), scale: scale)
        let image = try render(scene(glass: true), scale: scale)
        let minX = direction == .rightToLeft ? size.width - 20 - face.width : 20
        let rect = CGRect(x: minX, y: (size.height - face.height) / 2, width: face.width, height: face.height)
        assertMatchesModel(image, in: rect, "\(direction) @\(Int(scale))x") { px, py in
            model(of: rect, radius: 24, over: plain, px: px, py: py)
        }
    }

    func testOffCenterLeftToRight() throws { try checkOffCenter(.leftToRight) }
    func testOffCenterRightToLeft() throws { try checkOffCenter(.rightToLeft) }
    func testOffCenterRightToLeftRetina() throws { try checkOffCenter(.rightToLeft, scale: 2) }

    // MARK: Layered backdrops

    /// The gradient with a translucent band and white text crossing the
    /// capsule's rim and caps.
    private var layered: some View {
        ZStack {
            gradient
            Rectangle()
                .fill(Color(.sRGB, red: 0.1, green: 0.2, blue: 0.9, opacity: 0.45))
                .frame(width: 520, height: 34)
                .rotationEffect(.degrees(-12))
            Text("Liquid Glass")
                .font(.system(size: 34, weight: .bold))
                .foregroundStyle(.white)
                .offset(x: 70, y: -30)
        }
    }

    /// Clear glass over a layered backdrop refracts exactly what the
    /// backdrop draws, edges of text and translucency included.
    private func checkLayeredClear(scale: CGFloat) throws {
        let plain = try render(GlassScene(material: .clear, appearance: .light) {
            GlassContent { layered }
            Color.clear
        }, scale: scale)
        let image = try render(GlassScene(material: .clear, appearance: .light) {
            GlassContent { layered }
            centered(capsule())
        }, scale: scale)
        assertMatchesModel(image, in: capsuleRect, "layered clear @\(Int(scale))x") { px, py in
            model(of: capsuleRect, radius: .capsule, over: plain, px: px, py: py)
        }
    }

    func testLayeredBackdropClear() throws { try checkLayeredClear(scale: 1) }
    func testLayeredBackdropClearRetina() throws { try checkLayeredClear(scale: 2) }

    /// Regular glass frosts the same layered backdrop into a soft blur:
    /// neighboring pixels on its face never step like sharp edges.
    func testLayeredBackdropRegularFrosts() throws {
        let rect = CGRect(x: 50, y: 30, width: 300, height: 140)
        for appearance in [GlassAppearance.light, .dark] {
            let image = try render(GlassScene(material: .regular, appearance: appearance) {
                GlassContent { layered }
                centered(GlassSurface(radius: 28) { Color.clear.frame(width: rect.width, height: rect.height) })
            })
            let step = maxStep(image, in: rect.insetBy(dx: 24, dy: 24))
            XCTAssertLessThanOrEqual(step.value, 0.06, "\(appearance): largest step between neighbors on frosted glass, at \(step.at)")
        }
    }

    /// A toolbar's clusters and a tab bar's lens mirror right to left. Over a
    /// uniform backdrop, with empty items, the whole render mirrors.
    func testBarsMirrorRightToLeft() throws {
        func bars(_ direction: LayoutDirection) -> some View {
            GlassScene(material: .clear, appearance: .light) {
                GlassContent { Color(white: 0.5) }
                VStack(spacing: 20) {
                    GlassToolbar {
                        Color.clear.frame(width: 64, height: 28)
                        GlassToolbarSpacer()
                        Color.clear.frame(width: 28, height: 28)
                        Color.clear.frame(width: 28, height: 28)
                        Color.clear.frame(width: 28, height: 28)
                    }
                    GlassTabBar(selection: .constant(1)) {
                        GlassTab("", value: 1)
                        GlassTab("", value: 2)
                        GlassTab("", value: 3)
                    }
                    .frame(width: 240)
                }
                .padding(.horizontal, 16)
            }
            .environment(\.layoutDirection, direction)
        }
        let ltr = try render(bars(.leftToRight), settle: true)
        let rtl = try render(bars(.rightToLeft), settle: true)
        var worst = 0.0, at = (0, 0)
        for py in 0..<ltr.height {
            for px in 0..<ltr.width {
                let d = (ltr.pixel(px, py) - rtl.pixel(ltr.width - 1 - px, py)).array.map(abs).max() ?? 0
                if d > worst { worst = d; at = (px, py) }
            }
        }
        XCTAssertLessThanOrEqual(worst, tolerance, "right to left is not the mirror image of left to right, at \(at)")
    }

    /// The progressive blur is strongest at the edge it grows toward, in
    /// either layout direction.
    func testProgressiveBlurEdgeFollowsLayoutDirection() throws {
        let strip = CGSize(width: 400, height: 100)
        for direction in [LayoutDirection.leftToRight, .rightToLeft] {
            let image = try render(GlassScene {
                GlassContent { wave(16) }
                HStack(spacing: 0) {
                    GlassProgressiveBlur(edge: .leading, size: 120)
                    Spacer(minLength: 0)
                }
            }
            .environment(\.layoutDirection, direction), size: strip)
            /// Red's range over one wavelength centered on `x`, against the sharp 0.6.
            func contrast(at x: Int) -> Double {
                let reds = (x - 8..<x + 8).map { image.pixel($0, 50).x }
                return ((reds.max() ?? 0) - (reds.min() ?? 0)) / 0.6
            }
            let edge = direction == .leftToRight ? 8 : 392
            let inner = direction == .leftToRight ? 112 : 288
            XCTAssertLessThan(contrast(at: edge), 0.1, "\(direction): sharp at the edge")
            XCTAssertGreaterThan(contrast(at: inner), 0.6, "\(direction): blurred on the inner side")
            XCTAssertGreaterThan(contrast(at: direction == .leftToRight ? 300 : 100), 0.95, "\(direction): sharp outside the strip")
        }
    }

    /// A Regular tab bar's lens frosts like the bar around it, so content
    /// beneath never shows through the selected tab sharply.
    func testRegularTabBarLensFrosts() throws {
        func bar(_ material: GlassMaterial) throws -> Bitmap {
            try render(GlassScene(material: .clear, appearance: .dark) {
                GlassContent {
                    // A sharp black-and-white edge under the middle tab.
                    HStack(spacing: 0) { Color.black; Color.white }
                }
                centered(
                    GlassTabBar(selection: .constant(2), material: material) {
                        // Titles of spaces: wide tabs with nothing drawn on them.
                        GlassTab("            ", value: 1)
                        GlassTab("            ", value: 2)
                        GlassTab("            ", value: 3)
                    }
                    .frame(width: 300)
                )
            }, settle: true)
        }
        // Across the edge, in the middle of the lens.
        let window = CGRect(x: 185, y: 97, width: 30, height: 6)
        let regular = maxStep(try bar(.regular), in: window)
        XCTAssertLessThanOrEqual(regular.value, 0.06, "the lens of a Regular bar is frosted, at \(regular.at)")
        let clear = maxStep(try bar(.clear), in: window)
        XCTAssertGreaterThan(clear.value, 0.3, "a Clear lens shows the edge sharply, at \(clear.at)")
    }

    // MARK: Labels on glass

    /// The most positive and most negative luminance change from `without`
    /// to `with`: where a label is drawn, it is lighter or darker than glass.
    private func labelChange(_ with: Bitmap, _ without: Bitmap) -> (lighter: Double, darker: Double) {
        var lighter = 0.0, darker = 0.0
        for py in 0..<with.height {
            for px in 0..<with.width {
                let a = with.pixel(px, py), b = without.pixel(px, py)
                let d = (a.x + a.y + a.z - b.x - b.y - b.z) / 3
                lighter = max(lighter, d)
                darker = min(darker, d)
            }
        }
        return (lighter, darker)
    }

    /// Labels on bars, menus and menu triggers follow a forced glass
    /// appearance, not the app's color scheme: white on dark glass in light
    /// mode, #111113 on light glass in dark mode.
    func testLabelsOnGlassFollowAForcedAppearance() throws {
        let morph = GlassMenuMorph()
        morph.open(motion: .none)
        /// Each probe with and without a label; the two lay out alike.
        func probes(label: Bool, appearance: GlassAppearance) -> [(String, AnyView)] {
            let item = label
                ? AnyView(Rectangle().fill(.foreground).frame(width: 20, height: 20))
                : AnyView(Color.clear.frame(width: 20, height: 20))
            return [
                ("toolbar", AnyView(centered(GlassToolbar { item }))),
                // A filled square against its outline: the fill is the label's color.
                ("tab bar", AnyView(centered(GlassTabBar(selection: .constant(0)) {
                    GlassTab("", systemImage: label ? "square.fill" : "square", value: 1)
                }))),
                // The menu forces the appearance itself, inside an automatic one.
                ("menu trigger", AnyView(centered(
                    GlassMenu(appearance: appearance) { GlassMenuItem("One") {} } label: { item }
                        .glassAppearance(.automatic)
                ))),
                ("menu panel", AnyView(GlassMenuPanel(
                    options: GlassSurfaceOptions(material: nil, appearance: nil, radius: 28, refraction: nil, tint: nil, interactive: true, motion: nil),
                    presenter: CGRect(x: 300, y: 20, width: 44, height: 44), presenterRadius: 22, scene: size,
                    morph: morph, dismiss: {}, content: item.frame(width: 160)
                ))),
            ]
        }
        for (scheme, appearance) in [(ColorScheme.light, GlassAppearance.dark), (.dark, .light)] {
            let with = probes(label: true, appearance: appearance), without = probes(label: false, appearance: appearance)
            for index in with.indices {
                func scene(_ view: AnyView) -> some View {
                    GlassScene(material: .clear, appearance: appearance) {
                        GlassContent { Color(white: scheme == .light ? 0.85 : 0.15) }
                        view
                    }
                    .environment(\.colorScheme, scheme)
                }
                let change = labelChange(try render(scene(with[index].1), settle: true), try render(scene(without[index].1), settle: true))
                let name = with[index].0
                if appearance == .dark {
                    XCTAssertGreaterThan(change.lighter, 0.12, "\(name): a light label on dark glass in light mode")
                    XCTAssertGreaterThan(change.darker, -0.05, "\(name): no dark label on dark glass")
                } else {
                    XCTAssertLessThan(change.darker, -0.12, "\(name): a dark label on light glass in dark mode")
                    XCTAssertLessThan(change.lighter, 0.05, "\(name): no light label on light glass")
                }
            }
        }
    }

    /// Content on tinted glass (surfaces, buttons, bars) defaults to white or
    /// #111113, whichever contrasts more with the tinted glass.
    func testContentOnTintedGlassContrasts() throws {
        for (tint, dark) in [(Color.yellow, true), (.mint, true), (.blue, false), (.red, false)] {
            let image = try render(GlassScene(material: .clear, appearance: .light) {
                GlassContent { Color(white: 0.5) }
                centered(VStack(spacing: 20) {
                    GlassSurface(tint: tint) { Rectangle().fill(.foreground).frame(width: 40, height: 40).padding(20) }
                    GlassButton(tint: tint, action: {}) { Rectangle().fill(.foreground).frame(width: 20, height: 20) }
                })
            })
            // The surface's square, then the button's.
            let button = Double(GlassControlMetrics.buttonHeight(.regular))
            let top = (Double(size.height) - (80 + 20 + button)) / 2
            for (px, py, name) in [(200, Int(top + 40), "surface"), (200, Int(top + 100 + button / 2), "button")] {
                let pixel = image.pixel(px, py)
                if dark {
                    XCTAssertLessThan(pixel.x, 0.1, "\(tint) \(name): dark content")
                } else {
                    XCTAssertGreaterThan(pixel.x, 0.95, "\(tint) \(name): white content")
                }
            }
        }
    }

    // MARK: Menus

    /// Right to left, the panel opens under its trigger with the trigger's
    /// left edge as its trailing edge, and its glass under its items: over a
    /// uniform backdrop the whole render is the mirror of left to right.
    func testMenuPanelMirrorsRightToLeft() throws {
        let morph = GlassMenuMorph()
        morph.open(motion: .none)
        func panel(_ direction: LayoutDirection) throws -> Bitmap {
            let trigger = direction == .leftToRight ? CGRect(x: 300, y: 20, width: 44, height: 44) : CGRect(x: 56, y: 20, width: 44, height: 44)
            return try render(GlassScene(material: .clear, appearance: .light) {
                GlassContent { Color(white: 0.5) }
                GlassMenuPanel(
                    options: GlassSurfaceOptions(material: nil, appearance: nil, radius: 28, refraction: nil, tint: nil, interactive: true, motion: nil),
                    presenter: trigger, presenterRadius: 22, scene: size, morph: morph, dismiss: {},
                    content: Color.clear.frame(width: 180, height: 90)
                )
            }
            .environment(\.layoutDirection, direction), settle: true)
        }
        let ltr = try panel(.leftToRight), rtl = try panel(.rightToLeft)
        var worst = 0.0, at = (0, 0)
        for py in 0..<ltr.height {
            for px in 0..<ltr.width {
                let d = (ltr.pixel(px, py) - rtl.pixel(ltr.width - 1 - px, py)).array.map(abs).max() ?? 0
                if d > worst { worst = d; at = (px, py) }
            }
        }
        XCTAssertLessThanOrEqual(worst, tolerance, "right to left is not the mirror image of left to right, at \(at)")
        // The glass is there: under the trigger's left edge, right to left.
        let gray = SIMD4<Double>(repeating: 0.5)
        XCTAssertGreaterThan((rtl.pixel(80, 120) - gray).array.map(abs).max() ?? 0, 0.02, "no glass under the right-to-left panel")
        XCTAssertLessThanOrEqual((rtl.pixel(330, 120) - SIMD4(gray.x, gray.y, gray.z, 1)).array.map(abs).max() ?? 0, tolerance, "glass where the left-to-right panel would be")
    }

    /// The scene of the menu render tests: tall enough that the panel's
    /// frosted layer stays inside it.
    private let menuScene = CGSize(width: 400, height: 300)

    /// A rendered menu panel and where its parts are, in points.
    private struct MenuRender {
        let image: Bitmap
        let panel: CGRect
        /// A highlighted row: an item drawn with ``GlassMenuItemStyle`` as
        /// a hovered, focused or pressed item draws, with `label`, a 20 pt
        /// square in the panel's foreground, standing in for its title.
        let row: CGRect
        let label: CGRect
        /// A ``GlassMenuItem`` with a filled square for its icon, at `icon`.
        let item: CGRect
        let icon: CGPoint
    }

    /// An open menu panel under a trigger at the top right, over a solid
    /// `backdrop`, in the color scheme of its appearance: a highlighted row,
    /// a plain item and an empty stretch of glass.
    private func renderMenuPanel(
        material: GlassMaterial? = nil, appearance: GlassAppearance, backdrop: SIMD3<Double>, tint: Color? = nil,
        colorScheme: ColorScheme? = nil, reduceTransparency: Bool = false
    ) throws -> MenuRender {
        let morph = GlassMenuMorph()
        morph.open(motion: .none)
        let trigger = CGRect(x: 300, y: 20, width: 44, height: 44)
        let inset = GlassMenuMetrics.inset
        let rowWidth: CGFloat = 180, rowHeight: CGFloat = 44, itemHeight = GlassMenuMetrics.itemHeight, filler: CGFloat = 60
        let width = max(GlassMenuMetrics.minWidth, rowWidth + 2 * inset), height = rowHeight + itemHeight + filler + 2 * inset
        let panel = CGRect(x: trigger.maxX - width, y: trigger.maxY + GlassMenuMetrics.offset, width: width, height: height)
        let row = CGRect(x: panel.minX + inset, y: panel.minY + inset, width: rowWidth, height: rowHeight)
        let label = CGRect(x: row.midX - 10, y: row.midY - 10, width: 20, height: 20)
        let item = CGRect(x: row.minX, y: row.maxY, width: rowWidth, height: itemHeight)
        let icon = CGPoint(x: item.minX + GlassMenuMetrics.itemPadding + GlassMenuMetrics.iconWidth / 2, y: item.midY)
        let ground = Color(.sRGB, red: backdrop.x, green: backdrop.y, blue: backdrop.z)
        let image = try render(GlassScene(material: .clear, appearance: appearance) {
            GlassContent { ground }
            GlassMenuPanel(
                options: GlassSurfaceOptions(material: material, appearance: nil, radius: 28, refraction: nil, tint: tint, interactive: true, motion: nil),
                presenter: trigger, presenterRadius: 22, scene: menuScene, morph: morph, dismiss: {},
                content: VStack(alignment: .leading, spacing: 0) {
                    Button {} label: {
                        Rectangle().fill(.foreground).frame(width: 20, height: 20).frame(width: rowWidth, height: rowHeight)
                    }
                    .buttonStyle(GlassMenuItemStyle(highlighted: true, radius: .points(Double(28 - inset))))
                    GlassMenuItem("Item", systemImage: "square.fill") {}
                        .frame(width: rowWidth, height: itemHeight)
                    Color.clear.frame(width: rowWidth, height: filler)
                }
            )
        }
        .environment(\.colorScheme, colorScheme ?? (appearance == .dark ? .dark : .light))
        .environment(\._accessibilityReduceTransparency, reduceTransparency), size: menuScene, settle: true)
        return MenuRender(image: image, panel: panel, row: row, label: label, item: item, icon: icon)
    }

    /// The pixels of a menu panel's face, past the rim's inner glow (4.5 pt
    /// inside the outline): those of the highlighted row clear of its ends
    /// and its label, and the plain glass clear of the row and the item.
    private func menuFace(_ menu: MenuRender) -> (plain: [(Int, Int)], highlighted: [(Int, Int)]) {
        let panel = menu.panel
        let shape = GlassOptics.shapeParameters(width: Double(panel.width), height: Double(panel.height), radius: 28)
        let band = menu.row.insetBy(dx: 24, dy: 3), label = menu.label.insetBy(dx: -2, dy: -2)
        let row = menu.row.insetBy(dx: -2, dy: -2), item = menu.item.insetBy(dx: -2, dy: -2)
        var plain: [(Int, Int)] = [], highlighted: [(Int, Int)] = []
        for py in Int(panel.minY)..<Int(panel.maxY) {
            for px in Int(panel.minX)..<Int(panel.maxX) {
                let point = CGPoint(x: Double(px) + 0.5, y: Double(py) + 0.5)
                let field = GlassOptics.shapeField(
                    x: point.x - Double(panel.midX), y: point.y - Double(panel.midY), width: Double(panel.width), height: Double(panel.height),
                    extent: shape.extent, exponent: shape.exponent)
                guard field.distance < -6, !label.contains(point) else { continue }
                if band.contains(point) {
                    highlighted.append((px, py))
                } else if !row.contains(point), !item.contains(point) {
                    plain.append((px, py))
                }
            }
        }
        return (plain, highlighted)
    }

    /// Checks a rendered menu panel: its stand-in label and its item's icon
    /// draw in `ink`; every plain face pixel matches `model` within
    /// `accuracy`, every highlighted one the model with `ink` mixed in at
    /// ``GlassOptics/menuHighlight``, and both keep at least `floor`
    /// contrast with the label. Returns the lowest contrast.
    @discardableResult
    private func assertMenuPanel(
        _ menu: MenuRender, ink: SIMD3<Double>, model: SIMD3<Double>, accuracy: Double, floor: Double, _ name: String,
        file: StaticString = #filePath, line: UInt = #line
    ) -> Double {
        let drawn = menu.image.pixel(Int(menu.label.midX), Int(menu.label.midY))
        assertClose(SIMD3(drawn.x, drawn.y, drawn.z).array, ink.array, accuracy: tolerance, "\(name): label color", file: file, line: line)
        let icon = menu.image.pixel(Int(menu.icon.x), Int(menu.icon.y))
        assertClose(SIMD3(icon.x, icon.y, icon.z).array, ink.array, accuracy: tolerance, "\(name): the item's icon color", file: file, line: line)
        let face = menuFace(menu)
        let highlight = model + (ink - model) * GlassOptics.menuHighlight
        var lowest = Double.infinity
        for (pixels, expected, kind) in [(face.plain, model, "glass"), (face.highlighted, highlight, "highlighted row")] {
            var low = Double.infinity, at = (0, 0), worst = 0.0, got = SIMD3<Double>()
            for (px, py) in pixels {
                let p = menu.image.pixel(px, py), glass = SIMD3(p.x, p.y, p.z)
                let ratio = contrastRatio(glass, ink)
                if ratio < low { low = ratio; at = (px, py) }
                let d = (glass - expected).array.map(abs).max() ?? 0
                if d > worst { worst = d; got = glass }
            }
            XCTAssertGreaterThan(pixels.count, 1_000, "\(name): \(kind) pixels", file: file, line: line)
            XCTAssertGreaterThanOrEqual(low, floor, "\(name): label contrast on the \(kind) at \(at)", file: file, line: line)
            XCTAssertLessThanOrEqual(worst, accuracy, "\(name): \(kind) \(got.array) against the model's \(expected.array)", file: file, line: line)
            lowest = min(lowest, low)
        }
        return lowest
    }

    /// The model's menu glass over a solid `backdrop`: the material color,
    /// the tint, then the open panel's backing mix.
    private func menuModel(_ backdrop: SIMD3<Double>, material: GlassMaterial, dark: Bool, tint: SIMD3<Double>? = nil) -> SIMD3<Double> {
        var rgb = GlassOptics.materialColor(backdrop, material: material, dark: dark)
        if let tint { rgb = GlassOptics.tintColor(rgb, tint: tint, material: material) }
        return GlassOptics.legibilityColor(rgb, dark: dark, amount: GlassOptics.menuLegibility)
    }

    /// Menu text reads over any backdrop (`spec/OPTICS.md` §3): untinted
    /// Regular and Clear menus, light and dark, over white and over black,
    /// draw their items in the appearance foreground and keep at least
    /// 5.2:1 with them, on plain glass and on a highlighted row (the model's
    /// lowest over these backdrops, 5.24:1, is Clear in dark appearance over
    /// white). Regular glass alone, a surface without the backing, gives
    /// white labels over white and `#111113` over black less than 2.5:1.
    func testMenuTextContrastsOverAnyBackdrop() throws {
        let white = SIMD3<Double>(1, 1, 1), black = SIMD3<Double>(0, 0, 0)
        var panel = CGSize.zero
        for material in [GlassMaterial.regular, .clear] {
            for appearance in [GlassAppearance.light, .dark] {
                for backdrop in [white, black] {
                    let dark = appearance == .dark
                    let ink = GlassOptics.menuForeground(tint: nil, material: material, dark: dark)
                    XCTAssertEqual(ink, dark ? white : lightGlassInk)
                    let menu = try renderMenuPanel(material: material, appearance: appearance, backdrop: backdrop)
                    panel = menu.panel.size
                    assertMenuPanel(menu, ink: ink, model: menuModel(backdrop, material: material, dark: dark), accuracy: tolerance, floor: 5.2,
                                    "\(material) \(appearance) menu over \(backdrop == white ? "white" : "black")")
                }
            }
        }
        // A Regular surface of a panel's size carries no backing.
        for (appearance, backdrop) in [(GlassAppearance.dark, white), (.light, black)] {
            let ink = appearance == .dark ? white : lightGlassInk
            let ground = Color(.sRGB, red: backdrop.x, green: backdrop.y, blue: backdrop.z)
            let surface = try render(GlassScene(material: .regular, appearance: appearance) {
                GlassContent { ground }
                GlassSurface(radius: 28) { Color.clear.frame(width: panel.width, height: panel.height) }
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            }, size: menuScene)
            let center = surface.pixel(Int(menuScene.width / 2), Int(menuScene.height / 2))
            XCTAssertLessThan(contrastRatio(SIMD3(center.x, center.y, center.z), ink), 2.5, "\(appearance) Regular surface without the menu backing")
        }
    }

    /// The platform's system blue and yellow.
    private var systemTints: [(name: String, color: Color)] {
        #if canImport(UIKit)
        [("systemBlue", Color(uiColor: .systemBlue)), ("systemYellow", Color(uiColor: .systemYellow))]
        #else
        [("systemBlue", Color(nsColor: .systemBlue)), ("systemYellow", Color(nsColor: .systemYellow))]
        #endif
    }

    /// A menu tinted with a system color takes `menuForeground`: `#111113`
    /// on light glass and white on dark, whichever the tint, where the tint
    /// alone would give a light blue menu white labels (2.1:1 over white).
    /// Over white and black, on plain glass and on a highlighted row, its
    /// items keep at least 5.2:1 with the system colors as they resolve in
    /// light mode (the colors of the spec's floor), also on dark glass; in
    /// dark mode, systemYellow's dark variant leaves 5.07:1 on a highlighted
    /// row of dark glass over white.
    func testTintedMenuLabels() throws {
        let white = SIMD3<Double>(1, 1, 1), black = SIMD3<Double>(0, 0, 0)
        for (name, color) in systemTints {
            for (appearance, scheme) in [(GlassAppearance.light, ColorScheme.light), (.dark, .light), (.dark, .dark)] {
                var environment = EnvironmentValues()
                environment.colorScheme = scheme
                let resolved = color.resolve(in: environment).srgb
                let tint = SIMD3(Double(resolved.x), Double(resolved.y), Double(resolved.z))
                let dark = appearance == .dark
                let ink = GlassOptics.menuForeground(tint: tint, material: .regular, dark: dark)
                XCTAssertEqual(ink, dark ? white : lightGlassInk, "\(name) \(appearance) menu label")
                // The light variants are the spec's colors; systemYellow's
                // dark variant on dark glass keeps 5:1.
                let floor = scheme == .dark && name == "systemYellow" ? 5 : 5.2
                for backdrop in [white, black] {
                    let menu = try renderMenuPanel(appearance: appearance, backdrop: backdrop, tint: color, colorScheme: scheme)
                    assertMenuPanel(menu, ink: ink, model: menuModel(backdrop, material: .regular, dark: dark, tint: tint),
                                    accuracy: tolerance, floor: floor,
                                    "\(name) \(tint.array) \(appearance) menu in \(scheme) mode over \(backdrop == white ? "white" : "black")")
                }
            }
        }
    }

    /// Under Reduce Transparency the panel draws the opaque fill
    /// (`spec/OPTICS.md` §8, `#F2F2F7` light and `#1C1C1E` dark at 94%), and
    /// the backing mixes into that fill as into the glass: light, about
    /// `#F4F4F7`, which an 8-bit readback tells apart from the plain fill;
    /// dark, the fill is the backing already. Labels keep more than 11:1 over
    /// the backdrops that are hardest on them, highlighted rows too.
    func testMenuUnderReduceTransparency() throws {
        for (appearance, backdrop) in [(GlassAppearance.dark, SIMD3<Double>(1, 1, 1)), (.light, SIMD3<Double>(0, 0, 0))] {
            let dark = appearance == .dark
            let ink = dark ? SIMD3<Double>(1, 1, 1) : lightGlassInk
            let fill = dark ? SIMD3<Double>(0x1C, 0x1C, 0x1E) / 255 : SIMD3<Double>(0xF2, 0xF2, 0xF7) / 255
            let mixed = GlassOptics.legibilityColor(fill, dark: dark, amount: GlassOptics.menuLegibility)
            let model = mixed * 0.94 + backdrop * 0.06
            let menu = try renderMenuPanel(appearance: appearance, backdrop: backdrop, reduceTransparency: true)
            // About one 8-bit step: the plain fill is 1.7 steps off in light.
            // 14.4:1 on plain glass and 11.2:1 on a highlighted row of the
            // dark menu over white, the lowest of these.
            assertMenuPanel(menu, ink: ink, model: model, accuracy: 1.0 / 255.0, floor: 11,
                            "reduce transparency \(appearance) menu over \(dark ? "white" : "black")")
        }
    }

    // MARK: Clipping, sparse backdrops, degenerate sizes

    /// A surface clipped by an ancestor refracts what it would refract
    /// unclipped: SwiftUI renders as much of the layer as the shader reads.
    func testClippedSurfaceMatchesUnclipped() throws {
        for material in [GlassMaterial.clear, .regular] {
            try checkClipped(material)
        }
    }

    private func checkClipped(_ material: GlassMaterial) throws {
        func scene(clipped: Bool) -> some View {
            GlassScene(material: material, appearance: .light) {
                GlassContent { gradient }
                VStack(spacing: 0) {
                    Color.clear.frame(height: 105)
                    // Only scene rows from 105 down show; the capsule spans 68…132.
                    ZStack(alignment: .top) {
                        capsule().offset(y: 68 - 105)
                    }
                    .frame(width: size.width, height: 95, alignment: .top)
                    .modifier(ClipIf(enabled: clipped))
                }
            }
        }
        let clipped = try render(scene(clipped: true))
        let unclipped = try render(scene(clipped: false))
        var worst = 0.0, at = (0, 0)
        for py in 105..<133 {
            for px in 90..<310 {
                let d = (clipped.pixel(px, py) - unclipped.pixel(px, py)).array.map(abs).max() ?? 0
                if d > worst { worst = d; at = (px, py) }
            }
        }
        XCTAssertLessThanOrEqual(worst, 2.0 / 255.0, "clipped \(material) glass differs from unclipped at \(at)")
    }

    /// Glass over a backdrop narrower than itself keeps its whole outline:
    /// where nothing is beneath, it is just the rim light.
    func testGlassOverAnEmptyBackdropKeepsItsRim() throws {
        let rect = CGRect(x: 50, y: 50, width: 300, height: 100)
        func scene(glass: Bool) -> some View {
            GlassScene(material: .clear, appearance: .light) {
                GlassContent { Color(white: 0.5).frame(width: 200).frame(maxWidth: .infinity) }
                Group {
                    if glass {
                        centered(GlassSurface(radius: .capsule) { Color.clear.frame(width: rect.width, height: rect.height) })
                    } else {
                        Color.clear
                    }
                }
            }
        }
        let plain = try render(scene(glass: false))
        let image = try render(scene(glass: true))
        assertMatchesModel(image, in: rect, "over a narrower backdrop") { px, py in
            model(of: rect, radius: .capsule, over: plain, px: px, py: py)
        }
        let rim = (50..<100).flatMap { px in (48..<153).map { image.pixel(px, $0).w } }.max() ?? 0
        XCTAssertGreaterThan(rim, 0.3, "the rim is drawn over the empty backdrop")
    }

    /// The copies of the backdrop inside glass know they are copies, so they
    /// take no input and draw no AppKit or UIKit views (which SwiftUI can't
    /// render into a shader's layer) and no glass of their own.
    func testBackdropCopiesKnowTheyAreCopies() throws {
        let image = try render(GlassScene(material: .clear, appearance: .light) {
            GlassContent { RoleProbe() }
            centered(GlassSurface(radius: 24) { Color.clear.frame(width: 120, height: 120) })
        })
        // Outside the glass the visible backdrop (green), inside it the copy (red).
        assertClose(image.pixel(20, 20).array, [0, 1, 0, 1], accuracy: tolerance, "the visible backdrop")
        let face = image.pixel(200, 100)
        XCTAssertGreaterThan(face.x, 0.6, "the copy under the glass draws as a copy: \(face.array)")
        XCTAssertLessThan(face.y, 0.2, "the copy under the glass draws as a copy: \(face.array)")
    }

    /// Regular glass over a `GlassScrollView` frosts the scrolled content
    /// alone: on macOS the scroll view's AppKit anchor (for wheel events)
    /// lives only in the visible backdrop, never in the glass's copy, where
    /// SwiftUI would draw it as a yellow placeholder.
    func testRegularOverScrollViewFrostsItsContent() throws {
        let position = GlassScrollPosition()
        let image = try render(GlassScene(material: .regular, appearance: .light) {
            GlassContent {
                // Transparent content over a gray ground, like a list of cards.
                GlassScrollView(position: position) {
                    Color.clear.frame(height: 600)
                }
                .background(Color(white: 0.6))
            }
            centered(GlassSurface(radius: .capsule) { Color.clear.frame(width: 200, height: 64) })
        })
        let face = image.pixel(200, 100)
        let spread = max(face.x, face.y, face.z) - min(face.x, face.y, face.z)
        XCTAssertLessThan(spread, 0.02, "Regular glass over a gray scroll view is gray: \(face.array)")
    }

    /// Empty and degenerate surfaces draw nothing at all.
    func testEmptySurfacesDrawNothing() throws {
        let faces = [CGSize(width: 0, height: 40), CGSize(width: 1, height: 1), CGSize(width: 1, height: 40), CGSize(width: 2, height: 2), .zero]
        for scale in [CGFloat(1), 2] {
            func scene(glass: Bool) -> some View {
                GlassScene(material: .clear, appearance: .light) {
                    GlassContent { Color(white: 0.5) }
                    HStack(spacing: 60) {
                        ForEach(Array(faces.enumerated()), id: \.offset) { _, face in
                            if glass {
                                GlassSurface(radius: 16) { Color.clear.frame(width: face.width, height: face.height) }
                            } else {
                                Color.clear.frame(width: face.width, height: face.height)
                            }
                        }
                    }
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
            let image = try render(scene(glass: true), scale: scale)
            let plain = try render(scene(glass: false), scale: scale)
            var worst = 0.0, at = (0, 0)
            for py in 0..<image.height {
                for px in 0..<image.width {
                    let d = (image.pixel(px, py) - plain.pixel(px, py)).array.map(abs).max() ?? 0
                    if d > worst { worst = d; at = (px, py) }
                }
            }
            XCTAssertLessThanOrEqual(worst, 2.0 / 255.0, "an empty surface drew something at \(at) @\(Int(scale))x")
        }
    }

    // MARK: Fallbacks

    /// Reduce Transparency: the opaque fill at 94% with the rim light.
    func testReduceTransparency() throws {
        let image = try render(GlassScene(material: .clear, appearance: .light) {
            GlassContent { gradient }
            centered(capsule())
        }
        .environment(\._accessibilityReduceTransparency, true))
        let fill = SIMD3<Double>(0xF2, 0xF2, 0xF7) / 255
        for (column, row) in probes {
            let x = Double(column) + 0.5 - 200, y = Double(row) + 0.5 - 100
            let w = Double(surface.width), h = Double(surface.height)
            let p = GlassOptics.shapeParameters(width: w, height: h, radius: .capsule)
            let f = GlassOptics.shapeField(x: x, y: y, width: w, height: h, extent: p.extent, exponent: p.exponent)
            let ga = 0.94 * GlassOptics.coverage(distance: f.distance, pixel: 1)
            let base = vectorBackdrop(x, y)
            let glass = SIMD4(fill.x * ga, fill.y * ga, fill.z * ga, ga) + base * (1 - ga)
            let rim = GlassOptics.rimOverlay(distance: f.distance, nx: f.nx, ny: f.ny, dark: false, pixel: 1)
            let model = rim + glass * (1 - rim.w)
            assertClose(image.pixel(column, row).array, model.array, accuracy: tolerance, "reduce transparency pixel (\(column), \(row))")
        }
    }

    /// Without a scene, glass draws the platform material with the rim light
    /// over it: never nothing.
    func testFallbackWithoutScene() throws {
        let image = try render(ZStack {
            Color(white: 0.3)
            capsule()
        })
        let plain = SIMD4<Double>(repeating: 0.3)
        let rim = image.pixel(200, 68), outside = image.pixel(60, 30)
        assertClose(outside.array, [plain.x, plain.y, plain.z, 1], accuracy: 0.02, "outside the fallback glass")
        XCTAssertGreaterThan(rim.x, outside.x + 0.1, "the fallback paints the specular rim")
    }

    // MARK: Comparing with the model

    /// The model's color of pixel `(px, py)` of a surface at `rect` over the
    /// backdrop-only render `plain`, sampled as the shader samples its layer,
    /// with the default refraction for the surface's size.
    private func model(of rect: CGRect, radius: GlassRadius, over plain: Bitmap, px: Int, py: Int) -> SIMD4<Double> {
        let s = plain.scale
        let x = (Double(px) + 0.5) / s, y = (Double(py) + 0.5) / s
        let width = Double(rect.width), height = Double(rect.height)
        return GlassOptics.glassPixel(
            x: x - Double(rect.midX), y: y - Double(rect.midY), width: width, height: height, radius: radius, dark: false,
            refraction: GlassOptics.defaultRefraction(width: width, height: height), pixel: 1 / s,
            sample: { dx, dy in plain.sample(x: x + dx, y: y + dy) }, base: plain.pixel(px, py)
        )
    }

    /// Compares every pixel of `rect` (in points), and two pixels around it,
    /// with `model`, and reports the worst one.
    private func assertMatchesModel(
        _ image: Bitmap, in rect: CGRect, accuracy: Double? = nil, _ label: String,
        file: StaticString = #filePath, line: UInt = #line, model: (Int, Int) -> SIMD4<Double>
    ) {
        let s = image.scale
        let x0 = max(0, Int((Double(rect.minX) * s).rounded(.down)) - 2), x1 = min(image.width, Int((Double(rect.maxX) * s).rounded(.up)) + 2)
        let y0 = max(0, Int((Double(rect.minY) * s).rounded(.down)) - 2), y1 = min(image.height, Int((Double(rect.maxY) * s).rounded(.up)) + 2)
        var worst = 0.0, at = (0, 0), got = SIMD4<Double>(), want = SIMD4<Double>()
        for py in y0..<y1 {
            for px in x0..<x1 {
                let pixel = image.pixel(px, py), expected = model(px, py)
                let d = (pixel - expected).array.map(abs).max() ?? 0
                if d > worst { worst = d; at = (px, py); got = pixel; want = expected }
            }
        }
        XCTAssertLessThanOrEqual(
            worst, accuracy ?? tolerance, "\(label): pixel \(at) is \(got.array) against the model's \(want.array)",
            file: file, line: line
        )
    }

    /// The largest difference between horizontally or vertically adjacent
    /// pixels in `rect` (points), over the color channels.
    private func maxStep(_ image: Bitmap, in rect: CGRect) -> (value: Double, at: (Int, Int)) {
        let s = image.scale
        var worst = 0.0, at = (0, 0)
        for py in Int(Double(rect.minY) * s)..<Int(Double(rect.maxY) * s) {
            for px in Int(Double(rect.minX) * s)..<Int(Double(rect.maxX) * s) {
                let a = image.pixel(px, py), b = image.pixel(px + 1, py), c = image.pixel(px, py + 1)
                let d = max(abs(a.x - b.x), abs(a.y - b.y), abs(a.z - b.z), abs(a.x - c.x), abs(a.y - c.y), abs(a.z - c.z))
                if d > worst { worst = d; at = (px, py) }
            }
        }
        return (worst, at)
    }

    // MARK: Rendering

    /// Renders `view` at `size` and `scale` and reads it back as sRGB.
    /// `settle` lets state set on appearance (a tab bar's lens) render first.
    private func render<V: View>(_ view: V, size: CGSize? = nil, scale: CGFloat = 1, settle: Bool = false) throws -> Bitmap {
        let frame = size ?? self.size
        let renderer = ImageRenderer(content: view.frame(width: frame.width, height: frame.height).environment(\.displayScale, scale))
        renderer.scale = scale
        if settle {
            _ = renderer.cgImage
            RunLoop.main.run(until: Date().addingTimeInterval(0.2))
        }
        let image = try XCTUnwrap(renderer.cgImage, "ImageRenderer produced no image")
        // LG_RENDER_DIR=/path saves every render, for a look at failures.
        if let directory = ProcessInfo.processInfo.environment["LG_RENDER_DIR"] {
            renders += 1
            let name = "\(name.filter { $0.isLetter || $0.isNumber })-\(renders).png"
            let url = URL(fileURLWithPath: directory).appendingPathComponent(name) as CFURL
            if let destination = CGImageDestinationCreateWithURL(url, "public.png" as CFString, 1, nil) {
                CGImageDestinationAddImage(destination, image, nil)
                CGImageDestinationFinalize(destination)
            }
        }
        return try Bitmap(image, scale: Double(scale))
    }
}

/// Clips to the view's bounds when `enabled`.
private struct ClipIf: ViewModifier {
    let enabled: Bool

    func body(content: Content) -> some View {
        if enabled { content.clipped() } else { content }
    }
}

/// An 8-bit premultiplied sRGB readback.
struct Bitmap {
    let width: Int
    let height: Int
    let scale: Double
    private let bytes: [UInt8]

    init(_ image: CGImage, scale: Double) throws {
        width = image.width
        height = image.height
        self.scale = scale
        var bytes = [UInt8](repeating: 0, count: width * height * 4)
        let context = try XCTUnwrap(CGContext(
            data: &bytes, width: width, height: height, bitsPerComponent: 8, bytesPerRow: width * 4,
            space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ))
        context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
        self.bytes = bytes
    }

    /// The premultiplied color of pixel `(x, y)`, clamped to the image.
    func pixel(_ x: Int, _ y: Int) -> SIMD4<Double> {
        let x = min(max(x, 0), width - 1), y = min(max(y, 0), height - 1)
        let i = (y * width + x) * 4
        return SIMD4(Double(bytes[i]), Double(bytes[i + 1]), Double(bytes[i + 2]), Double(bytes[i + 3])) / 255
    }

    /// The unpremultiplied color at a point (in points), interpolated
    /// bilinearly between pixel centers, as the shader samples its layer.
    func sample(x: Double, y: Double) -> SIMD4<Double> {
        let fx = x * scale - 0.5, fy = y * scale - 0.5
        let x0 = Int(fx.rounded(.down)), y0 = Int(fy.rounded(.down))
        let tx = fx - Double(x0), ty = fy - Double(y0)
        let top = pixel(x0, y0) * (1 - tx) + pixel(x0 + 1, y0) * tx
        let bottom = pixel(x0, y0 + 1) * (1 - tx) + pixel(x0 + 1, y0 + 1) * tx
        let c = top * (1 - ty) + bottom * ty
        return c.w > 1e-5 ? SIMD4(c.x / c.w, c.y / c.w, c.z / c.w, c.w) : .zero
    }
}

/// Green in the visible backdrop, red in the copies inside glass, blue
/// outside any scene.
private struct RoleProbe: View {
    @Environment(\.glassBackdropRole) private var role

    var body: some View {
        switch role {
        case .copy: Color(.sRGB, red: 1, green: 0, blue: 0)
        case .primary: Color(.sRGB, red: 0, green: 1, blue: 0)
        case .none: Color(.sRGB, red: 0, green: 0, blue: 1)
        }
    }
}
