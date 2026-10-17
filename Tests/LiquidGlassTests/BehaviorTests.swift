@testable import LiquidGlass
import SwiftUI
import XCTest

/// Control logic without rendering: the tab lens, drag intent, scroll
/// physics, reduced motion, tint labels, sizes and API shape.
@MainActor
final class BehaviorTests: XCTestCase {
    private func run(_ client: GlassAnimating, seconds: Double = 3, each: (() -> Void)? = nil) {
        for _ in 0..<Int(seconds * 120) {
            _ = client.advance(by: 1.0 / 120.0)
            each?()
        }
    }

    // MARK: Tab lens

    /// Right to left, the tabs' frames run from right to left in declaration
    /// order; dragging the lens still chooses the tab under the finger.
    func testTabLensDragRightToLeft() {
        let one = CGRect(x: 216, y: 4, width: 100, height: 54)
        let two = CGRect(x: 110, y: 4, width: 100, height: 54)
        let three = CGRect(x: 4, y: 4, width: 100, height: 54)
        let lens = TabLens()
        lens.tabs = [TabFrame(value: 1, rect: one), TabFrame(value: 2, rect: two), TabFrame(value: 3, rect: three)]
        lens.select(one, motion: .full)
        for step in 0...20 {
            lens.drag(to: one.midX + (two.midX - one.midX) * CGFloat(step) / 20, start: one.midX, motion: .full)
            _ = lens.advance(by: 1.0 / 60.0)
        }
        run(lens, seconds: 1)
        XCTAssertEqual(lens.presentation.midX, two.midX, accuracy: 6, "the lens follows the finger")
        XCTAssertEqual(lens.endDrag(), AnyHashable(2))
    }

    /// Reduced motion moves the lens on the critically damped track spring:
    /// it travels over several frames and never overshoots.
    func testTabLensReducedMotionGlidesWithoutOvershoot() {
        let lens = TabLens()
        let a = CGRect(x: 4, y: 4, width: 100, height: 54), b = CGRect(x: 216, y: 4, width: 100, height: 54)
        lens.select(a, motion: .reduced)
        lens.select(b, motion: .reduced)
        _ = lens.advance(by: 1.0 / 60.0)
        XCTAssertLessThan(lens.presentation.minX, b.minX - 20, "the lens moves, it doesn't jump")
        var furthest = 0.0
        run(lens) { furthest = max(furthest, lens.presentation.minX) }
        XCTAssertLessThanOrEqual(furthest, b.minX + 0.01, "no overshoot")
        XCTAssertEqual(lens.presentation.minX, b.minX, accuracy: 0.01)
    }

    func testTabLensWithoutMotionJumps() {
        let lens = TabLens()
        let a = CGRect(x: 4, y: 4, width: 100, height: 54), b = CGRect(x: 216, y: 4, width: 100, height: 54)
        lens.select(a, motion: .none)
        lens.select(b, motion: .none)
        XCTAssertEqual(lens.presentation, b)
    }

    /// Right to left, the arrow keys follow the tabs as they appear: the
    /// declaration order runs from right to left.
    func testTabsInVisualOrderRightToLeft() {
        let lens = TabLens()
        lens.tabs = [
            TabFrame(value: 1, rect: CGRect(x: 216, y: 4, width: 100, height: 54)),
            TabFrame(value: 2, rect: CGRect(x: 110, y: 4, width: 100, height: 54)),
            TabFrame(value: 3, rect: CGRect(x: 4, y: 4, width: 100, height: 54)),
        ]
        XCTAssertEqual(lens.valuesInVisualOrder, [AnyHashable(3), AnyHashable(2), AnyHashable(1)])
        lens.tabs.reverse()
        XCTAssertEqual(lens.valuesInVisualOrder, [AnyHashable(3), AnyHashable(2), AnyHashable(1)])
    }

    /// The lens stretches with its speed while it moves, but its refraction
    /// is measured on the tab it rests at.
    func testTabLensRefractsAtItsRestingSize() {
        let lens = TabLens()
        let a = CGRect(x: 4, y: 4, width: 100, height: 54), b = CGRect(x: 216, y: 4, width: 120, height: 54)
        lens.select(a, motion: .full)
        lens.select(b, motion: .full)
        var stretched = false
        run(lens, seconds: 1) {
            stretched = stretched || lens.presentation.height < b.height - 1
            XCTAssertEqual(lens.restingSize, b.size)
        }
        XCTAssertTrue(stretched, "the lens stretches while it moves")
    }

    // MARK: Refraction

    /// Unset, refraction follows the resting size (`spec/OPTICS.md` §2); one
    /// set on the scene or the environment applies at every size; a
    /// surface's own wins over both.
    func testRefractionResolution() {
        let button = CGSize(width: 120, height: 34), card = CGSize(width: 320, height: 200)
        var environment = EnvironmentValues()
        XCTAssertNil(environment.glassRefraction)
        XCTAssertEqual(environment.resolvedGlassRefraction(nil, restingSize: button), 25.5)
        XCTAssertEqual(environment.resolvedGlassRefraction(nil, restingSize: card), 60)
        XCTAssertEqual(environment.resolvedGlassRefraction(7.75, restingSize: CGSize(width: 37, height: 24)), 7.75)
        XCTAssertEqual(environment.resolvedGlassRefraction(-4, restingSize: button), 0)
        environment.glassRefraction = 60
        XCTAssertEqual(environment.resolvedGlassRefraction(nil, restingSize: button), 60)
        XCTAssertEqual(environment.resolvedGlassRefraction(5, restingSize: button), 5)
        environment.glassRefraction = nil
        XCTAssertEqual(environment.resolvedGlassRefraction(nil, restingSize: button), 25.5)
    }

    // MARK: Switch thumb

    func testSwitchReducedMotionGlidesWithoutOvershoot() {
        let thumb = GlassThumbMotion()
        thumb.setPosition(0, animated: true, motion: .reduced)
        thumb.setPosition(1, animated: true, motion: .reduced)
        _ = thumb.advance(by: 1.0 / 60.0)
        XCTAssertLessThan(thumb.position, 0.5, "the thumb travels, it doesn't jump")
        var furthest = 0.0
        run(thumb) { furthest = max(furthest, thumb.position) }
        XCTAssertLessThanOrEqual(furthest, 1.0001, "no overshoot")
        XCTAssertEqual(thumb.position, 1, accuracy: 1e-3)
    }

    func testThumbLiftCrossfadesUnderReducedMotion() {
        let thumb = GlassThumbMotion()
        thumb.setPressed(true, motion: .reduced)
        _ = thumb.advance(by: 0.05)
        XCTAssertGreaterThan(thumb.progress, 0)
        XCTAssertLessThan(thumb.progress, 1, "the lift fades in over 200 ms")
        run(thumb, seconds: 0.5)
        XCTAssertEqual(thumb.progress, 1)
        XCTAssertEqual(thumb.stretch, 0)
    }

    // MARK: Glow

    /// The light follows the critically damped glow spring when pressed.
    func testPressGlowNeverOvershoots() {
        let interaction = GlassInteraction()
        interaction.size = CGSize(width: 120, height: 44)
        interaction.pointerChanged(local: CGPoint(x: 60, y: 22))
        var brightest = 0.0
        run(interaction, seconds: 1) { brightest = max(brightest, interaction.glow) }
        XCTAssertLessThanOrEqual(brightest, 1.0001)
        XCTAssertEqual(interaction.glow, 1, accuracy: 1e-3)
        interaction.pointerEnded()
    }

    /// A press lights the glass on the glow spring (OPTICS §6), as the
    /// Android port does, not on the press spring.
    func testPressGlowFollowsTheGlowSpring() {
        for keyboard in [false, true] {
            let interaction = GlassInteraction()
            interaction.size = CGSize(width: 120, height: 44)
            if keyboard {
                interaction.keyboardPress(true)
            } else {
                interaction.pointerChanged(local: CGPoint(x: 60, y: 22))
            }
            var state = GlassSpring.State(value: 0, target: 1)
            for _ in 0..<12 {
                _ = interaction.advance(by: 1.0 / 120.0)
                state = GlassSpring.glow.step(state, dt: 1.0 / 120.0)
            }
            XCTAssertEqual(interaction.glow, state.value, accuracy: 1e-9, keyboard ? "keyboard press" : "pointer press")
            XCTAssertLessThan(interaction.glow, 1)
        }
    }

    // MARK: Menu

    private final class MenuProbe {
        var items: [GlassMenuItemEntry] = []
        var ran: [String] = []
    }

    /// A disabled item publishes no entry: the arrow keys skip it and Return
    /// can't run it.
    func testMenuKeyboardSkipsDisabledItems() throws {
        let probe = MenuProbe()
        let panel = GlassMenuPanelContext(highlighted: nil, itemRadius: 12, choose: { $0() })
        let renderer = ImageRenderer(content: VStack {
            GlassMenuItem("One") { probe.ran.append("one") }
            GlassMenuItem("Two") { probe.ran.append("two") }.disabled(true)
            GlassMenuItem("Three", checked: true) { probe.ran.append("three") }
        }
        .environment(\.glassMenuPanel, panel)
        .backgroundPreferenceValue(GlassMenuItemKey.self) { items in
            let _ = { probe.items = items }()
            Color.clear
        }
        .frame(width: 240, height: 200))
        _ = try XCTUnwrap(renderer.cgImage)
        let items = probe.items
        XCTAssertEqual(items.count, 2, "the disabled item has no entry")
        let first = GlassMenuNavigation.item(after: nil, by: 1, in: items)
        XCTAssertEqual(first, items[0].id)
        let second = GlassMenuNavigation.item(after: first, by: 1, in: items)
        XCTAssertEqual(second, items[1].id, "down skips the disabled item")
        XCTAssertEqual(GlassMenuNavigation.item(after: second, by: 1, in: items), items[0].id, "and wraps")
        XCTAssertEqual(GlassMenuNavigation.item(after: nil, by: -1, in: items), items[1].id)
        XCTAssertNil(GlassMenuNavigation.item(after: nil, by: 1, in: []))
        for item in items { item.action() }
        XCTAssertEqual(probe.ran, ["one", "three"])
    }

    // MARK: Drag intent

    func testDragIntentCommitsSidewaysAndDeclinesVertical() {
        let sideways = GlassDragIntent(distance: 8)
        XCTAssertNil(sideways.update(CGPoint(x: 10, y: 10)))
        XCTAssertNil(sideways.update(CGPoint(x: 15, y: 11)))
        XCTAssertEqual(sideways.update(CGPoint(x: 20, y: 12)), CGPoint(x: 10, y: 10))
        XCTAssertTrue(sideways.isCommitted)
        XCTAssertEqual(sideways.update(CGPoint(x: 20, y: 80)), CGPoint(x: 10, y: 10), "committed for the rest of the touch")

        let vertical = GlassDragIntent(distance: 8)
        _ = vertical.update(CGPoint(x: 10, y: 10))
        XCTAssertNil(vertical.update(CGPoint(x: 13, y: 22)))
        XCTAssertNil(vertical.update(CGPoint(x: 60, y: 22)), "a scroll stays a scroll")
        XCTAssertFalse(vertical.isCommitted)
        XCTAssertNil(vertical.pendingStart)

        let tap = GlassDragIntent(distance: 8)
        _ = tap.update(CGPoint(x: 30, y: 5))
        XCTAssertEqual(tap.pendingStart, CGPoint(x: 30, y: 5))
        tap.reset()
        XCTAssertNil(tap.pendingStart)

        let pointer = GlassDragIntent(distance: 0)
        XCTAssertEqual(pointer.update(CGPoint(x: 3, y: 4)), CGPoint(x: 3, y: 4), "a pointer press commits at once")
    }

    // MARK: Scrolling

    private func scrolled() -> GlassScrollPosition {
        let position = GlassScrollPosition()
        position.axes = .vertical
        position.setSizes(content: CGSize(width: 400, height: 2000), viewport: CGSize(width: 400, height: 800))
        return position
    }

    /// A drag the system cancels (no `onEnded`) still ends: the content
    /// settles and the display link can stop.
    func testCancelledDragSettles() {
        let position = scrolled()
        // A drag reports its translation from where it began.
        position.drag(translation: .zero)
        position.drag(translation: CGSize(width: 0, height: -120))
        XCTAssertTrue(position.isScrolling)
        XCTAssertEqual(position.offset.y, 120, accuracy: 0.01)
        position.endDrag(velocity: .zero)
        XCTAssertFalse(position.isScrolling)
        XCTAssertFalse(position.advance(by: 1.0 / 60.0))

        position.scrollTo(.zero, animated: false)
        position.drag(translation: .zero)
        position.drag(translation: CGSize(width: 0, height: 300))
        XCTAssertLessThan(position.offset.y, 0, "rubber-banded past the top")
        position.endDrag(velocity: .zero)
        var frames = 0
        while position.advance(by: 1.0 / 60.0) && frames < 600 { frames += 1 }
        XCTAssertLessThan(frames, 600, "the display link stops")
        XCTAssertFalse(position.isScrolling)
        XCTAssertEqual(position.offset.y, 0, accuracy: 0.01)

        // A drag cancelled mid-content stays where it is, and the next drag
        // starts from there.
        position.drag(translation: .zero)
        position.drag(translation: CGSize(width: 0, height: -200))
        position.endDrag(velocity: .zero)
        XCTAssertFalse(position.advance(by: 1.0 / 60.0))
        position.drag(translation: .zero)
        XCTAssertEqual(position.offset.y, 200, accuracy: 0.01, "a new drag starts from the current offset")
        position.drag(translation: CGSize(width: 0, height: -50))
        XCTAssertEqual(position.offset.y, 250, accuracy: 0.01)
        position.endDrag(velocity: .zero)
    }

    /// A finger landing after a drag whose end was lost lets go of that drag.
    func testTouchReleasesAStaleDrag() {
        let position = scrolled()
        position.drag(translation: .zero)
        position.drag(translation: CGSize(width: 0, height: 200))
        XCTAssertLessThan(position.offset.y, 0)
        position.touchDown()
        position.touchUp()
        var frames = 0
        while position.advance(by: 1.0 / 60.0) && frames < 600 { frames += 1 }
        XCTAssertFalse(position.isScrolling)
        XCTAssertEqual(position.offset.y, 0, accuracy: 0.01)
    }

    func testKeyboardScrollingAccumulatesAndClamps() {
        let position = scrolled()
        position.scroll(byKeyboard: CGSize(width: 0, height: 720))
        position.scroll(byKeyboard: CGSize(width: 0, height: 720))
        var frames = 0
        while position.advance(by: 1.0 / 60.0) && frames < 600 { frames += 1 }
        XCTAssertEqual(position.offset.y, 1200, accuracy: 0.01, "two pages, clamped to the end")
    }

    #if os(macOS)
    private func wheel(_ dy: Int32, phase: Int64, momentum: Int64 = 0) throws -> NSEvent {
        let cg = try XCTUnwrap(CGEvent(scrollWheelEvent2Source: nil, units: .pixel, wheelCount: 1, wheel1: dy, wheel2: 0, wheel3: 0))
        cg.setIntegerValueField(.scrollWheelEventIsContinuous, value: 1)
        cg.setIntegerValueField(.scrollWheelEventScrollPhase, value: phase)
        cg.setIntegerValueField(.scrollWheelEventMomentumPhase, value: momentum)
        return try XCTUnwrap(NSEvent(cgEvent: cg))
    }

    /// A trackpad flick: its momentum keeps belonging to the scroll view
    /// wherever the pointer goes (the monitor routes it even outside the
    /// view), and its end leaves the content at rest.
    func testTrackpadMomentumEndsTheGesture() throws {
        let position = scrolled()
        let monitor = WheelMonitor()
        // CGScrollPhase: began 1, changed 2, ended 4. Momentum: begin 1, continue 2, end 3.
        let began = try wheel(-10, phase: 1), changed = try wheel(-30, phase: 2), lifted = try wheel(0, phase: 4)
        XCTAssertEqual(began.phase, .began)
        XCTAssertFalse(monitor.continuesGesture(changed), "nothing under way yet")
        monitor.handle(began, position: position)
        monitor.handle(changed, position: position)
        XCTAssertTrue(position.isScrolling)
        monitor.handle(lifted, position: position)
        let coasting = try wheel(-20, phase: 0, momentum: 2)
        XCTAssertEqual(coasting.momentumPhase, .changed)
        XCTAssertTrue(monitor.continuesGesture(coasting), "momentum belongs to the gesture outside the view too")
        monitor.handle(try wheel(-20, phase: 0, momentum: 1), position: position)
        monitor.handle(coasting, position: position)
        XCTAssertTrue(position.isScrolling)
        monitor.handle(try wheel(0, phase: 0, momentum: 3), position: position)
        XCTAssertFalse(position.isScrolling, "the momentum's end ends the gesture")
        XCTAssertFalse(position.advance(by: 1.0 / 60.0), "the display link can stop")
        XCTAssertFalse(monitor.continuesGesture(coasting))
        XCTAssertGreaterThan(position.offset.y, 0)
    }
    #endif

    // MARK: Tint labels

    /// Labels on tinted glass take white or #111113, whichever contrasts
    /// more with the glass the tint draws: white on blue, red and purple,
    /// dark on yellow, mint, green, orange and cyan.
    func testLabelsOnTintStayLegible() {
        let environment = EnvironmentValues()
        func isDark(_ color: Color, _ material: GlassMaterial = .clear) -> Bool {
            GlassForeground.onTint(color.resolve(in: environment).srgb, material: material) == GlassForeground.light
        }
        for color in [Color.yellow, .mint, .cyan, .green, .orange] {
            XCTAssertTrue(isDark(color), "\(color) takes a dark label")
            XCTAssertTrue(isDark(color, .regular), "\(color) takes a dark label on Regular glass")
        }
        // Fixed blue, red, purple and indigo (the iOS 17 and macOS 14 system
        // colors): system colors change between releases, and iOS 26's
        // lighter blue sits within 0.1% of a tie on Regular glass.
        let saturated = [
            Color(red: 0, green: 0.478, blue: 1), Color(red: 1, green: 0.231, blue: 0.188),
            Color(red: 0.686, green: 0.322, blue: 0.871), Color(red: 0.345, green: 0.337, blue: 0.839),
            Color(red: 1, green: 0.176, blue: 0.333),
        ]
        for color in saturated {
            XCTAssertFalse(isDark(color), "\(color) takes a white label")
            XCTAssertFalse(isDark(color, .regular), "\(color) takes a white label on Regular glass")
        }
        XCTAssertTrue(isDark(.white))
        XCTAssertFalse(isDark(.black))
    }

    /// The chosen label always has the higher WCAG contrast with the glass.
    func testTintLabelHasTheHigherContrast() {
        let dark = GlassForeground.relativeLuminance(17 / 255, 17 / 255, 19 / 255)
        for step in 0...40 {
            let v = Float(step) / 40
            for tint in [SIMD3<Float>(v, v, v), SIMD3(v, 0.4, 1 - v), SIMD3(1, v, 0)] {
                let pass = 0.68 + 0.32 * 0.5
                let glass = GlassForeground.relativeLuminance(Double(tint.x) * pass, Double(tint.y) * pass, Double(tint.z) * pass)
                let onWhite = 1.05 / (glass + 0.05), onDark = (glass + 0.05) / (dark + 0.05)
                let expected = onDark > onWhite ? GlassForeground.light : GlassForeground.dark
                XCTAssertEqual(GlassForeground.onTint(tint, material: .clear), expected, "tint \(tint)")
            }
        }
    }

    // MARK: Sizes

    private final class SizeBox { var size = CGSize.zero }

    private func measure<V: View>(_ view: V) -> CGSize {
        let box = SizeBox()
        let renderer = ImageRenderer(content: GlassScene {
            GlassContent { Color.gray }
            view
                .fixedSize()
                .background(GeometryReader { proxy in
                    let _ = { box.size = proxy.size }()
                    Color.clear
                })
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .frame(width: 600, height: 400))
        _ = renderer.cgImage
        return box.size
    }

    #if os(macOS)
    private let iconButton: CGFloat = 28
    private let switchTrack = CGSize(width: 54, height: 24)
    #else
    private let iconButton: CGFloat = 44
    private let switchTrack = CGSize(width: 63, height: 28)
    #endif

    /// An icon-only menu trigger is the icon button circle.
    func testIconMenuTriggerIsACircle() {
        let menu = measure(GlassMenu(icon: "ellipsis", label: "More") { GlassMenuItem("One") {} })
        let button = measure(GlassButton(icon: "ellipsis", label: "More") {})
        XCTAssertEqual(menu, CGSize(width: iconButton, height: iconButton))
        XCTAssertEqual(button, menu)
    }

    func testSwitchWithHiddenLabelHugsItsTrack() throws {
        guard #available(iOS 18.0, macOS 15.0, *) else { throw XCTSkip("labelsVisibility needs iOS 18 or macOS 15") }
        XCTAssertEqual(measure(GlassSwitch("Hidden", isOn: .constant(true)).labelsHidden()), switchTrack)
        XCTAssertGreaterThan(measure(GlassSwitch("Shown", isOn: .constant(true))).width, switchTrack.width + 20)
    }

    /// Bars stop growing with the largest text sizes, with their labels.
    func testBarsStopGrowingAtAccessibilitySizes() {
        func heights(_ size: DynamicTypeSize) -> (CGFloat, CGFloat) {
            let toolbar = measure(GlassToolbar { GlassButton(icon: "plus", label: "Add") {} }.dynamicTypeSize(size))
            let tabs = measure(GlassTabBar(selection: .constant(0)) { GlassTab("Home", systemImage: "house", value: 0) }.dynamicTypeSize(size))
            return (toolbar.height, tabs.height)
        }
        let capped = heights(.xxxLarge), largest = heights(.accessibility5)
        XCTAssertEqual(largest.0, capped.0, "toolbar")
        XCTAssertEqual(largest.1, capped.1, "tab bar")
        XCTAssertGreaterThanOrEqual(heights(.large).1, GlassControlMetrics.tabBarHeight(.regular))
    }

    /// The bar's size when it is offered `proposal`, for example a tall
    /// row in a two-column layout.
    private func measure<V: View>(_ view: V, offered proposal: CGSize) -> CGSize {
        let box = SizeBox()
        let renderer = ImageRenderer(content: GlassScene {
            GlassContent { Color.gray }
            view
                .background(GeometryReader { proxy in
                    let _ = { box.size = proxy.size }()
                    Color.clear
                })
                .frame(width: proposal.width, height: proposal.height)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .frame(width: 600, height: 400))
        _ = renderer.cgImage
        return box.size
    }

    /// Offered more height than they need, bars keep their own height
    /// instead of stretching into tall capsules.
    func testBarsKeepTheirHeightWhenOfferedMore() {
        let tall = CGSize(width: 320, height: 240)
        let toolbar = measure(GlassToolbar {
            GlassButton(icon: "chevron.left", label: "Back") {}
            GlassToolbarSpacer()
            GlassButton(icon: "bold", label: "Bold") {}
            GlassToolbarSpacer(.fixed)
            GlassButton(icon: "plus", label: "Add") {}
        }, offered: tall)
        XCTAssertEqual(toolbar.height, GlassControlMetrics.toolbarHeight, "toolbar")
        let tabs = measure(GlassTabBar(selection: .constant(0)) { GlassTab("Home", systemImage: "house", value: 0) }, offered: tall)
        XCTAssertEqual(tabs.height, GlassControlMetrics.tabBarHeight(.regular), "tab bar")
    }

    /// The glass layer's size is animatable, so a surface that SwiftUI
    /// animates to a new size keeps its outline on the content's animated
    /// frame (layout reports only the final size).
    func testGlassLayerSizeAnimates() {
        let style = GlassRenderStyle(
            material: .clear, dark: false, radius: .capsule, refraction: 60, tint: nil, nested: false,
            increaseContrast: false, reduceTransparency: false, pixel: 1
        )
        var layer = GlassLayer(size: CGSize(width: 120, height: 40), style: style, outline: GlassOutlineState(), padding: 10, localBackdrop: nil)
        let from = layer.animatableData
        var to = from
        to.first = 300
        // Halfway through, as an animation interpolates it.
        var halfway = from
        halfway.first += (to.first - from.first) * 0.5
        layer.animatableData = halfway
        XCTAssertEqual(layer.size, CGSize(width: 210, height: 40))
    }

    // MARK: API shape

    /// Labels computed at runtime, roles and the shared options compile.
    func testInitializersAcceptRuntimeStringsAndSharedOptions() {
        let name = String("Close")
        _ = GlassButton(icon: "xmark", label: name) {}
        _ = GlassMenu(icon: "ellipsis", label: name) {
            GlassMenuItem(name, checked: true) {}
            GlassMenuItem("Delete", role: .destructive) {}
        }
        _ = GlassButton("Delete", systemImage: "trash", role: .destructive) {}
        _ = GlassButton(
            "Static", appearance: .dark, refraction: 20, tint: .blue, interactive: false, motion: .reduced
        ) {}
        _ = GlassButton(role: .cancel, radius: 12, interactive: false, action: {}) { Text("Custom") }
        _ = GlassMenu(icon: "ellipsis", label: "More", appearance: .dark, tint: .blue) { GlassMenuItem("One") {} }
    }
}
