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
    /// Resolves glass options against this environment.
    func glassStyle(_ options: GlassSurfaceOptions, defaultMaterial: GlassMaterial? = nil) -> GlassRenderStyle {
        let increaseContrast = colorSchemeContrast == .increased
        let material = increaseContrast ? .regular : (options.material ?? defaultMaterial ?? glassMaterial)
        return GlassRenderStyle(
            material: material,
            dark: (options.appearance ?? glassAppearance).isDark(in: colorScheme),
            radius: options.radius,
            refraction: max(0, options.refraction ?? glassRefraction),
            tint: options.tint.map { $0.resolve(in: self).srgb },
            nested: glassNested,
            increaseContrast: increaseContrast,
            reduceTransparency: accessibilityReduceTransparency,
            pixel: 1 / Double(max(displayScale, 1))
        )
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
/// view's bounds when `rect` is `nil`.
struct GlassOutlineShape: Shape {
    var rect: CGRect?
    var radius: GlassRadius

    func path(in bounds: CGRect) -> Path {
        let frame = rect.map { $0.offsetBy(dx: bounds.minX, dy: bounds.minY) } ?? bounds
        return GlassShape(radius: radius).path(in: frame)
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
    /// Fades the glass (not the content) in and out.
    var opacity: Double = 1
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
        let style = environment.glassStyle(options, defaultMaterial: defaultMaterial)
        let motion = environment.glassMotion(options.motion)
        let interactive = options.interactive && environment.isEnabled
        let explicitAppearance = (options.appearance ?? environment.glassAppearance) != .automatic
        return content
            .environment(\.glassNested, true)
            .transformEnvironment(\.colorScheme) { scheme in
                if explicitAppearance { scheme = style.dark ? .dark : .light }
            }
            .foregroundStyle(GlassForeground.color(dark: style.dark))
            .clipShape(GlassOutlineShape(rect: outline, radius: style.radius))
            .modifier(GlassPressTransform(model: interactive ? model : nil))
            .background {
                GeometryReader { proxy in
                    GlassLayerHost(
                        size: proxy.size, style: style, motion: motion, outline: outline, opacity: opacity,
                        padding: GlassRendering.padding(
                            size: outline?.size ?? proxy.size, refraction: style.refraction,
                            frosted: style.material == .regular, interactive: interactive, extra: extraPadding),
                        localBackdrop: localBackdrop, model: interactive || interaction != nil ? model : nil
                    )
                }
            }
            .contentShape(GlassOutlineShape(rect: outline, radius: style.radius))
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
        let state = GlassOutlineState(
            rect: outline,
            scale: model?.scale ?? CGSize(width: 1, height: 1),
            translation: model?.translation ?? .zero,
            glow: model?.glow ?? 0,
            glowPoint: model?.glowPoint ?? .zero,
            opacity: opacity
        )
        GlassLayer(size: size, style: style, outline: state, padding: padding, localBackdrop: localBackdrop)
            .onChange(of: outline?.size ?? size, initial: true) { _, newSize in model?.size = newSize }
            .onChange(of: motion, initial: true) { _, newMotion in model?.motion = newMotion }
    }
}

/// Scales and moves content with the press, about its center.
private struct GlassPressTransform: ViewModifier {
    let model: GlassInteraction?

    func body(content: Content) -> some View {
        if let model {
            content
                .scaleEffect(x: model.scale.width, y: model.scale.height)
                .offset(model.translation)
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
                    ended: { model.pointerEnded() }
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
    let ended: () -> Void

    func body(content: Content) -> some View {
        #if canImport(UIKit)
        if #available(iOS 18.0, *) {
            content.gesture(GlassTouchTracker(
                space: space,
                changed: { local, global in
                    changed(local)
                    changedGlobally(global)
                },
                ended: ended
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
                if !isTracking { ended() }
            }
            .simultaneousGesture(
                DragGesture(minimumDistance: 0, coordinateSpace: space)
                    .updating(tracking) { _, state, _ in state = true }
                    .onChanged { changed($0.location) }
                    .onEnded { _ in ended() }
            )
            .simultaneousGesture(
                DragGesture(minimumDistance: 0, coordinateSpace: .global)
                    .onChanged { changedGlobally($0.location) }
            )
    }
}
