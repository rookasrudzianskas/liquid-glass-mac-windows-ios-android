import SwiftUI

/// The backdrop that glass in a ``GlassScene`` refracts.
///
/// `GlassContent` must be the first element of a scene. Everything after it
/// is the scene's foreground, where glass surfaces can sit at any depth:
///
/// ```swift
/// GlassScene {
///     GlassContent {
///         Image("aurora").resizable().scaledToFill()
///     }
///     .ignoresSafeArea()
///
///     VStack {
///         GlassSurface { Text("Hello") }.padding()
///     }
/// }
/// ```
///
/// The content renders once as the visible backdrop, and once more (clipped)
/// inside every glass surface. Keep it a pure function of external state:
/// state the content stores in `@State` is separate in every copy, and
/// UIKit- or AppKit-backed views (native `ScrollView`, `List`, `TextField`,
/// `Map`, video) can't be drawn into glass. Use ``GlassScrollView`` to scroll
/// a backdrop.
public struct GlassContent<Content: View> {
    let content: Content
    let ignoredSafeAreaEdges: Edge.Set

    /// Creates the backdrop layer of a scene.
    public init(@ViewBuilder content: () -> Content) {
        self.content = content()
        self.ignoredSafeAreaEdges = []
    }

    private init(content: Content, ignoredSafeAreaEdges: Edge.Set) {
        self.content = content
        self.ignoredSafeAreaEdges = ignoredSafeAreaEdges
    }

    /// Extends the backdrop under the safe area on `edges`, while the scene's
    /// foreground keeps respecting it.
    public func ignoresSafeArea(_ edges: Edge.Set = .all) -> GlassContent {
        GlassContent(content: content, ignoredSafeAreaEdges: edges)
    }
}

/// The backdrop and foreground of a scene, as built by ``GlassSceneBuilder``.
public struct GlassSceneLayers<Backdrop: View, Foreground: View> {
    let content: GlassContent<Backdrop>
    let foreground: Foreground
}

/// Builds a ``GlassScene``: a ``GlassContent`` followed by the foreground views.
///
/// The builder accepts a fixed sequence of views. Put conditional or looping
/// content inside a container such as `ZStack` or `Group`.
@resultBuilder
public enum GlassSceneBuilder {
    public static func buildBlock<B: View>(_ content: GlassContent<B>) -> GlassSceneLayers<B, EmptyView> {
        GlassSceneLayers(content: content, foreground: EmptyView())
    }

    public static func buildBlock<B: View, F: View>(_ content: GlassContent<B>, _ foreground: F) -> GlassSceneLayers<B, F> {
        GlassSceneLayers(content: content, foreground: foreground)
    }

    public static func buildBlock<B: View, F0: View, F1: View, each F: View>(
        _ content: GlassContent<B>, _ f0: F0, _ f1: F1, _ rest: repeat each F
    ) -> GlassSceneLayers<B, TupleView<(F0, F1, repeat each F)>> {
        GlassSceneLayers(content: content, foreground: TupleView((f0, f1, repeat each rest)))
    }
}

/// The region glass may refract.
///
/// A scene renders its ``GlassContent`` as the backdrop and lays its
/// foreground over it, like a `ZStack`. Every glass surface in the foreground,
/// at any depth (including inside a native `ScrollView`), refracts the backdrop
/// at its exact on-screen position, also while it moves, scales or animates.
///
/// A scene fills the space it is offered, like `GeometryReader`. Its
/// material, appearance, refraction and motion become the defaults of every
/// glass element inside it.
public struct GlassScene<Backdrop: View, Foreground: View>: View {
    private let layers: GlassSceneLayers<Backdrop, Foreground>
    private let material: GlassMaterial?
    private let appearance: GlassAppearance?
    private let refraction: Double?
    private let motion: GlassMotion?

    @Environment(\.self) private var environment
    @Namespace private var space

    /// Creates a scene.
    /// - Parameters:
    ///   - material: The default material inside the scene, or `nil` to inherit.
    ///   - appearance: The default appearance inside the scene, or `nil` to inherit.
    ///   - refraction: The default optical travel at the rim, or `nil` to inherit.
    ///   - motion: The default motion level, or `nil` to inherit.
    ///   - content: A ``GlassContent`` followed by the foreground.
    public init(
        material: GlassMaterial? = nil,
        appearance: GlassAppearance? = nil,
        refraction: Double? = nil,
        motion: GlassMotion? = nil,
        @GlassSceneBuilder content: () -> GlassSceneLayers<Backdrop, Foreground>
    ) {
        self.layers = content()
        self.material = material
        self.appearance = appearance
        self.refraction = refraction
        self.motion = motion
    }

    public var body: some View {
        GeometryReader { proxy in
            let backdrop = makeBackdrop(size: proxy.size, insets: proxy.safeAreaInsets)
            ZStack(alignment: .topLeading) {
                layers.content.content
                    .environment(\.glassBackdropRole, .primary)
                    .frame(width: backdrop.rect.width, height: backdrop.rect.height)
                    .offset(x: backdrop.rect.minX, y: backdrop.rect.minY)
                layers.foreground
                    .frame(width: proxy.size.width, height: proxy.size.height)
                    .overlayPreferenceValue(GlassOverlayKey.self) { items in
                        GlassOverlayLayer(items: items)
                    }
                    .environment(\.glassBackdrop, backdrop)
                    .environment(\.glassOverlayAvailable, true)
            }
        }
        .coordinateSpace(.named(space))
        .transformEnvironment(\.self) { values in
            if let material { values.glassMaterial = material }
            if let appearance { values.glassAppearance = appearance }
            if let refraction { values.glassRefraction = max(0, refraction) }
            if let motion { values.glassMotion = motion }
        }
    }

    private func makeBackdrop(size: CGSize, insets: EdgeInsets) -> GlassBackdrop {
        let edges = layers.content.ignoredSafeAreaEdges
        let leading = edges.contains(.leading) ? insets.leading : 0
        let trailing = edges.contains(.trailing) ? insets.trailing : 0
        let top = edges.contains(.top) ? insets.top : 0
        let bottom = edges.contains(.bottom) ? insets.bottom : 0
        let rect = CGRect(x: -leading, y: -top, width: size.width + leading + trailing, height: size.height + top + bottom)
        return GlassBackdrop(content: AnyView(layers.content.content), environment: environment, rect: rect, space: space)
    }
}

/// A scene's backdrop as its glass surfaces see it.
struct GlassBackdrop {
    /// The backdrop content.
    let content: AnyView
    /// The environment the visible backdrop renders with.
    let environment: EnvironmentValues
    /// The backdrop's frame in the scene's coordinate space.
    let rect: CGRect
    /// The name of the scene's coordinate space.
    let space: Namespace.ID
}

/// A copy of the scene's backdrop that lines up with the visible one wherever
/// it is placed: it measures its own on-screen frame at render time and moves
/// (and, under an ancestor's scale, rescales) the content back into place, so
/// it stays aligned while surfaces are dragged or animated.
struct GlassBackdropCopy: View {
    let backdrop: GlassBackdrop

    var body: some View {
        let rect = backdrop.rect
        let space = backdrop.space
        backdrop.content
            .environment(\.self, backdrop.environment)
            .environment(\.glassBackdropRole, .copy)
            .frame(width: rect.width, height: rect.height)
            .allowsHitTesting(false)
            .accessibilityHidden(true)
            .visualEffect { content, proxy in
                let frame = proxy.frame(in: .named(space))
                let sx = proxy.size.width > 0 ? frame.width / proxy.size.width : 1
                let sy = proxy.size.height > 0 ? frame.height / proxy.size.height : 1
                return content
                    .offset(x: rect.minX - frame.minX, y: rect.minY - frame.minY)
                    .scaleEffect(x: 1 / max(sx, 1e-3), y: 1 / max(sy, 1e-3), anchor: .topLeading)
            }
    }
}
