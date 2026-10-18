import SwiftUI

/// The options every glass element shares (`spec/COMPONENTS.md`, Shared options).
/// `nil` values inherit from the environment.
struct GlassSurfaceOptions {
    var material: GlassMaterial?
    var appearance: GlassAppearance?
    var radius: GlassRadius
    var refraction: Double?
    var tint: Color?
    var interactive: Bool
    var motion: GlassMotion?
}

extension EnvironmentValues {
    /// Resolves glass options against this environment for a surface whose
    /// resting size (before press and stretch) is `restingSize`.
    /// `legibility` is the mix toward the legibility backing (menu panels).
    func glassStyle(
        _ options: GlassSurfaceOptions, defaultMaterial: GlassMaterial? = nil, restingSize: CGSize, legibility: Double = 0
    ) -> GlassRenderStyle {
        let increaseContrast = colorSchemeContrast == .increased
        let material = increaseContrast ? .regular : (options.material ?? defaultMaterial ?? glassMaterial)
        return GlassRenderStyle(
            material: material,
            dark: (options.appearance ?? glassAppearance).isDark(in: colorScheme),
            radius: options.radius,
            refraction: resolvedGlassRefraction(options.refraction, restingSize: restingSize),
            tint: options.tint.map { $0.resolve(in: self).srgb },
            nested: glassNested,
            increaseContrast: increaseContrast,
            reduceTransparency: accessibilityReduceTransparency,
            pixel: 1 / Double(max(displayScale, 1)),
            legibility: min(1, max(0, legibility))
        )
    }

    /// The refraction a surface draws with (`spec/OPTICS.md` §2): its own,
    /// else the one set on the scene or the environment, else the default
    /// for its resting size.
    func resolvedGlassRefraction(_ requested: Double?, restingSize: CGSize) -> Double {
        if let explicit = requested ?? glassRefraction { return max(0, explicit) }
        return GlassOptics.defaultRefraction(width: Double(restingSize.width), height: Double(restingSize.height))
    }

    /// The motion level after Reduce Motion.
    func glassMotion(_ requested: GlassMotion?) -> GlassMotion {
        (requested ?? glassMotion).resolved(reduceMotion: accessibilityReduceMotion)
    }
}

extension Color.Resolved {
    /// Gamma-encoded sRGB channels, clamped to `0...1`.
    var srgb: SIMD3<Float> {
        func encode(_ linear: Float) -> Float {
            let a = abs(linear)
            let gamma = a <= 0.0031308 ? 12.92 * a : 1.055 * pow(a, 1 / 2.4) - 0.055
            return min(1, max(0, linear < 0 ? -gamma : gamma))
        }
        return SIMD3(encode(linearRed), encode(linearGreen), encode(linearBlue))
    }
}

/// The outline of a surface: its continuous-corner shape in `rect`, or in the
/// view's bounds when `rect` is `nil`. `rect` is measured from the left edge,
/// like the geometry it comes from, so the shape never mirrors.
struct GlassOutlineShape: Shape {
    var rect: CGRect?
    var radius: GlassRadius

    var layoutDirectionBehavior: LayoutDirectionBehavior { .fixed }

    func path(in bounds: CGRect) -> Path {
        let frame = rect.map { $0.offsetBy(dx: bounds.minX, dy: bounds.minY) } ?? bounds
        return GlassShape(radius: radius).path(in: frame)
    }
}

/// Text and icons drawn on glass: the glass foreground (`#111113` on light
/// glass, white on dark, `spec/OPTICS.md` §8), or white or dark for contrast
/// with tinted glass. A forced light or dark appearance also sets their color
/// scheme, so `.primary` and `.secondary` adapt. Surfaces apply it to their
/// content; bars and menus to the items drawn over their glass, which are
/// not a surface's content.
struct GlassForegroundStyle: ViewModifier {
    let options: GlassSurfaceOptions
    var defaultMaterial: GlassMaterial?
    /// How far the glass under the content mixes toward the legibility
    /// backing when it is fully there: menu panels pass
    /// ``GlassOptics/menuLegibility``, so their items take
    /// ``GlassOptics/menuForeground(tint:material:dark:legibility:)``, and
    /// keep it while the panel opens and closes.
    var legibility: Double = 0

    @Environment(\.self) private var environment

    func body(content: Content) -> some View {
        // Only the colors are used, and they don't depend on the size.
        let style = environment.glassStyle(options, defaultMaterial: defaultMaterial, restingSize: .zero)
        let explicit = (options.appearance ?? environment.glassAppearance) != .automatic
        content
            .transformEnvironment(\.colorScheme) { scheme in
                if explicit { scheme = style.dark ? .dark : .light }
            }
            .foregroundStyle(foreground(style))
    }

    private func foreground(_ style: GlassRenderStyle) -> Color {
        if let tint = style.tint {
            return GlassForeground.onTint(tint, material: style.material, dark: style.dark, legibility: legibility)
        }
        return GlassForeground.color(dark: style.dark)
    }
}

/// The engine behind every glass element: content over a glass layer, with
/// the elastic interaction of `spec/OPTICS.md` §6 when `interactive` is set.
struct GlassSurfaceCore<Content: View>: View {
    var options: GlassSurfaceOptions
    /// The material when neither the options nor the environment set one
    /// explicitly; menus pass `.regular`.
    var defaultMaterial: GlassMaterial?
    /// Overrides the outline, in local points (menus morphing, selection lenses).
    var outline: CGRect?
    /// The size an unset refraction is measured on, when it isn't the
    /// outline's (or, without one, the bounds'): a tab lens that stretches
    /// with its speed rests at its tab's size, and a menu panel whose
    /// outline grows out of its trigger rests open. Presses and drags scale
    /// the outline in the shader and never change it.
    var restingSize: CGSize?
    /// Fades the glass (not the content) in and out.
    var opacity: Double = 1
    /// How far the glass mixes toward the solid legibility backing
    /// (`spec/OPTICS.md` §3): menu panels pass ``GlassOptics/menuLegibility``
    /// times their open progress; every other surface keeps 0.
    var legibility: Double = 0
    /// Extra room around the surface for outlines that grow past its bounds.
    var extraPadding: CGFloat = 0
    /// Views the glass refracts in addition to the scene's backdrop.
    var localBackdrop: AnyView?
    /// An interaction owned by a parent (toolbar clusters, tab lenses).
    var interaction: GlassInteraction?
    /// Whether the surface attaches its own touch and pointer gestures.
    /// Buttons attach them outside SwiftUI's `Button` instead, so the
    /// button's own gesture keeps working.
    var handlesInput = true
    var content: Content

    @State private var ownInteraction = GlassInteraction()
    @Environment(\.self) private var environment
    @GestureState private var tracking = false

    private var model: GlassInteraction { interaction ?? ownInteraction }

    var body: some View {
        if environment.glassBackdropRole == .copy {
            // Inside another surface's backdrop copy, glass would recurse.
            content
        } else {
            surface
        }
    }

    private var surface: some View {
        let motion = environment.glassMotion(options.motion)
        let interactive = options.interactive && environment.isEnabled
        return content
            .environment(\.glassNested, true)
            .modifier(GlassForegroundStyle(options: options, defaultMaterial: defaultMaterial))
            .clipShape(GlassOutlineShape(rect: outline, radius: options.radius))
            .modifier(GlassPressTransform(model: interactive ? model : nil))
            .background {
                GeometryReader { proxy in
                    let shape = outline?.size ?? proxy.size
                    let style = environment.glassStyle(
                        options, defaultMaterial: defaultMaterial, restingSize: restingSize ?? shape, legibility: legibility)
                    GlassLayerHost(
                        size: proxy.size, style: style, motion: motion, outline: outline, opacity: opacity,
                        padding: GlassRendering.padding(
                            size: shape, refraction: style.refraction,
                            frosted: style.material == .regular, interactive: interactive, extra: extraPadding),
                        localBackdrop: localBackdrop, model: interactive || interaction != nil ? model : nil
                    )
                }
                // The glass works in left-to-right coordinates, as measured
                // geometry, touches and the shader do in every layout direction.
                .environment(\.layoutDirection, .leftToRight)
            }
            .contentShape(GlassOutlineShape(rect: outline, radius: options.radius))
            .modifier(GlassPressGestures(model: interactive && handlesInput ? model : nil, tracking: $tracking))
    }
}

/// Reads the interaction once per frame and hands the glass layer its state.
private struct GlassLayerHost: View {
    let size: CGSize
    let style: GlassRenderStyle
    let motion: GlassMotion
    let outline: CGRect?
    let opacity: Double
    let padding: CGFloat
    let localBackdrop: AnyView?
    let model: GlassInteraction?

    var body: some View {
        layer
            .onChange(of: outline?.size ?? size, initial: true) { _, newSize in model?.size = newSize }
            .onChange(of: motion, initial: true) { _, newMotion in model?.motion = newMotion }
    }

    @ViewBuilder
    private var layer: some View {
        let shape = outline?.size ?? size
        if min(shape.width, shape.height) < GlassRendering.minimumSide {
            // An empty or degenerate outline (empty text, collapsed content,
            // a first layout pass) has no glass; the rim around it would
            // draw a stray line or dot.
            Color.clear
        } else {
            GlassLayer(
                size: size, style: style,
                outline: GlassOutlineState(
                    rect: outline,
                    scale: model?.scale ?? CGSize(width: 1, height: 1),
                    translation: model?.translation ?? .zero,
                    glow: model?.glow ?? 0,
                    glowPoint: model?.glowPoint ?? .zero,
                    opacity: opacity
                ),
                padding: padding, localBackdrop: localBackdrop
            )
        }
    }
}

/// Scales and moves content with the press, about its center.
private struct GlassPressTransform: ViewModifier {
    let model: GlassInteraction?

    @Environment(\.layoutDirection) private var layoutDirection

    func body(content: Content) -> some View {
        if let model {
            // The translation is left to right, like the touches it follows;
            // SwiftUI mirrors offsets right to left.
            let x = layoutDirection == .rightToLeft ? -model.translation.width : model.translation.width
            content
                .scaleEffect(x: model.scale.width, y: model.scale.height)
                .offset(x: x, y: model.translation.height)
        } else {
            content
        }
    }
}

/// Touch, pointer and hover input for an interactive surface. A cancelled
/// gesture (for example when a scroll view takes over) resets `tracking`,
/// which ends the press.
struct GlassPressGestures: ViewModifier {
    let model: GlassInteraction?
    let tracking: GestureState<Bool>

    func body(content: Content) -> some View {
        if let model {
            content
                .modifier(GlassTouchInput(
                    space: LocalCoordinateSpace.local, tracking: tracking,
                    changed: { model.pointerChanged(local: $0) },
                    changedGlobally: { model.pointerChanged(global: $0) },
                    ended: { _ in model.pointerEnded() }
                ))
                .onContinuousHover(coordinateSpace: .local) { phase in
                    switch phase {
                    case .active(let location): model.hover(location)
                    case .ended: model.hover(nil)
                    }
                }
        } else {
            content
        }
    }
}

/// Reports a finger or pointer pressing the view, in `space` and in the
/// global space, without taking the touch from buttons or scroll views.
struct GlassTouchInput<Space: CoordinateSpaceProtocol>: ViewModifier {
    let space: Space
    let tracking: GestureState<Bool>
    /// The press began or moved, in `space`.
    let changed: (CGPoint) -> Void
    /// The same location in the global space.
    let changedGlobally: (CGPoint) -> Void
    /// The press ended: `true` when the finger lifted, `false` when the
    /// touch was cancelled or taken over (for example by a scroll view). It
    /// may be reported twice, once of each kind.
    let ended: (_ completed: Bool) -> Void
    /// While this returns `true`, the press owns the touch: an enclosing
    /// scroll view that starts to pan is stopped instead of the press (a
    /// slider or tab lens being dragged sideways).
    var claimsTouch: () -> Bool = { false }

    func body(content: Content) -> some View {
        #if canImport(UIKit)
        if #available(iOS 18.0, *) {
            content.gesture(GlassTouchTracker(
                space: space,
                changed: { local, global in
                    changed(local)
                    changedGlobally(global)
                },
                ended: ended,
                claimsTouch: claimsTouch
            ))
        } else {
            drags(content)
        }
        #else
        drags(content)
        #endif
    }

    /// Two simultaneous drags: one in `space` and one in global coordinates,
    /// which don't move with the view.
    private func drags(_ content: Content) -> some View {
        content
            .onChange(of: tracking.wrappedValue) { _, isTracking in
                if !isTracking { ended(false) }
            }
            .simultaneousGesture(
                DragGesture(minimumDistance: 0, coordinateSpace: space)
                    .updating(tracking) { _, state, _ in state = true }
                    .onChanged { changed($0.location) }
                    .onEnded { _ in ended(true) }
            )
            .simultaneousGesture(
                DragGesture(minimumDistance: 0, coordinateSpace: .global)
                    .onChanged { changedGlobally($0.location) }
            )
    }
}
