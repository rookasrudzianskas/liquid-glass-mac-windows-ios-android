import SwiftUI
#if canImport(AppKit) && !targetEnvironment(macCatalyst)
import AppKit
#endif

/// A scroll view drawn entirely by SwiftUI, so glass can refract it.
///
/// Native `ScrollView` and `List` are backed by UIKit and AppKit views,
/// which SwiftUI cannot draw into a shader effect; inside a
/// ``GlassContent`` they would vanish from every surface's backdrop.
/// `GlassScrollView` moves its content with an offset instead: drag to
/// scroll (iOS), with UIScrollView's deceleration and rubber band past the
/// ends; trackpad and mouse wheel on macOS. Its ``GlassScrollPosition`` is
/// shared by the visible backdrop and its copies, so glass always refracts
/// the content exactly where it is.
///
/// ```swift
/// @State private var scroll = GlassScrollPosition()
///
/// GlassScene {
///     GlassContent {
///         GlassScrollView(position: scroll) {
///             LazyVStack { ForEach(articles) { ArticleRow($0) } }
///         }
///     }
///     .ignoresSafeArea()
///
///     VStack { GlassToolbar { … }; Spacer() }
/// }
/// ```
///
/// Content is laid out at its ideal length along the scrolling axes (`LazyVStack`
/// works but renders every row). The scroll view honors VoiceOver's
/// three-finger scroll.
public struct GlassScrollView<Content: View>: View {
    private let axes: Axis.Set
    private let position: GlassScrollPosition
    private let showsIndicators: Bool
    private let content: Content

    @Environment(\.glassBackdropRole) private var role

    /// Creates a scroll view.
    /// - Parameters:
    ///   - axes: The scrollable axes. Default vertical.
    ///   - position: The shared scroll position.
    ///   - showsIndicators: Whether a scroll indicator appears while scrolling.
    ///   - content: The scrolled content.
    public init(
        _ axes: Axis.Set = .vertical, position: GlassScrollPosition, showsIndicators: Bool = true,
        @ViewBuilder content: () -> Content
    ) {
        self.axes = axes
        self.position = position
        self.showsIndicators = showsIndicators
        self.content = content()
    }

    public var body: some View {
        let interactive = role != .copy
        GeometryReader { viewport in
            content
                .fixedSize(horizontal: axes.contains(.horizontal), vertical: axes.contains(.vertical))
                .frame(
                    minWidth: axes.contains(.horizontal) ? nil : viewport.size.width,
                    maxWidth: axes.contains(.horizontal) ? nil : viewport.size.width,
                    minHeight: axes.contains(.vertical) ? nil : viewport.size.height,
                    maxHeight: axes.contains(.vertical) ? nil : viewport.size.height,
                    alignment: .topLeading
                )
                .background {
                    if interactive {
                        GeometryReader { measured in
                            Color.clear.onChange(of: Sizes(content: measured.size, viewport: viewport.size), initial: true) { _, sizes in
                                position.axes = axes
                                position.setSizes(content: sizes.content, viewport: sizes.viewport)
                            }
                        }
                    }
                }
                .modifier(ScrollOffset(position: position))
                .frame(width: viewport.size.width, height: viewport.size.height, alignment: .topLeading)
                .overlay(alignment: axes.contains(.vertical) ? .topTrailing : .bottomLeading) {
                    if showsIndicators {
                        ScrollIndicator(position: position, vertical: axes.contains(.vertical))
                    }
                }
                .modifier(ScrollInput(position: position, enabled: interactive, frame: viewport.frame(in: .global)))
        }
        .clipped()
        .accessibilityScrollAction { edge in
            let page = position.viewportSize.height * 0.9
            switch edge {
            case .top: position.scrollTo(CGPoint(x: position.offset.x, y: position.offset.y - page))
            case .bottom: position.scrollTo(CGPoint(x: position.offset.x, y: position.offset.y + page))
            case .leading: position.scrollTo(CGPoint(x: position.offset.x - position.viewportSize.width * 0.9, y: position.offset.y))
            case .trailing: position.scrollTo(CGPoint(x: position.offset.x + position.viewportSize.width * 0.9, y: position.offset.y))
            }
        }
    }

    private struct Sizes: Equatable {
        let content: CGSize
        let viewport: CGSize
    }
}

/// Moves the content by the shared offset; only this modifier redraws per frame.
private struct ScrollOffset: ViewModifier {
    let position: GlassScrollPosition

    func body(content: Content) -> some View {
        content.offset(x: -position.offset.x, y: -position.offset.y)
    }
}

/// A thin capsule that shows where the viewport is while the content moves.
private struct ScrollIndicator: View {
    let position: GlassScrollPosition
    let vertical: Bool

    var body: some View {
        let viewport = vertical ? position.viewportSize.height : position.viewportSize.width
        let content = vertical ? position.contentSize.height : position.contentSize.width
        let offset = vertical ? position.offset.y : position.offset.x
        let maximum = max(1, content - viewport)
        let track = max(0, viewport - 12)
        // Shorter while rubber-banding past either end, like the system's.
        let overshoot = offset < 0 ? -offset : max(0, offset - maximum)
        let length = max(24, track * viewport / max(content, 1) - overshoot)
        let travel = min(max(0, offset / maximum), 1) * max(0, track - length)
        Capsule()
            .fill(.secondary)
            .frame(width: vertical ? 3 : length, height: vertical ? length : 3)
            .offset(x: vertical ? -3 : 6 + travel, y: vertical ? 6 + travel : -3)
            .opacity(position.isScrolling && content > viewport ? 1 : 0)
            .animation(.easeOut(duration: position.isScrolling ? 0.1 : 0.6), value: position.isScrolling)
            .allowsHitTesting(false)
            .accessibilityHidden(true)
    }
}

/// Touch and scroll-wheel input for the visible scroll view (copies inside
/// glass only display the shared position).
private struct ScrollInput: ViewModifier {
    let position: GlassScrollPosition
    let enabled: Bool
    let frame: CGRect

    func body(content: Content) -> some View {
        if enabled {
            content
                .contentShape(Rectangle())
                #if os(macOS)
                .background(ScrollWheelMonitor(position: position, frame: frame))
                #else
                .simultaneousGesture(
                    DragGesture(minimumDistance: 0)
                        .onChanged { value in
                            if value.translation == .zero { position.touchDown() }
                        }
                )
                .gesture(
                    DragGesture(minimumDistance: 8)
                        .onChanged { position.drag(translation: $0.translation) }
                        .onEnded { position.endDrag(velocity: $0.velocity) }
                )
                #endif
        } else {
            content.allowsHitTesting(false)
        }
    }
}

#if os(macOS)
/// Routes the window's scroll-wheel events over `frame` to the position.
/// Trackpads deliver precise deltas and their own momentum; mouse wheels
/// deliver lines.
private struct ScrollWheelMonitor: View {
    let position: GlassScrollPosition
    let frame: CGRect

    @State private var monitor = WheelMonitor()

    var body: some View {
        Color.clear
            .onAppear { monitor.start(position: position) }
            .onDisappear { monitor.stop() }
            .onChange(of: frame, initial: true) { _, frame in monitor.frame = frame }
    }
}

@MainActor
private final class WheelMonitor {
    var frame = CGRect.zero
    private var token: Any?
    /// Bumped by every event, so a pending end is dropped when momentum follows.
    private var generation = 0

    func start(position: GlassScrollPosition) {
        guard token == nil else { return }
        token = NSEvent.addLocalMonitorForEvents(matching: .scrollWheel) { [weak self, weak position] event in
            guard let self, let position, let view = event.window?.contentView else { return event }
            var point = view.convert(event.locationInWindow, from: nil)
            if !view.isFlipped { point.y = view.bounds.height - point.y }
            guard self.frame.contains(point) else { return event }
            self.handle(event, position: position)
            return nil
        }
    }

    private func handle(_ event: NSEvent, position: GlassScrollPosition) {
        generation += 1
        guard event.hasPreciseScrollingDeltas else {
            let line: CGFloat = 16
            position.scroll(by: CGSize(width: -event.scrollingDeltaX * line, height: -event.scrollingDeltaY * line), phase: .step)
            return
        }
        position.scroll(by: CGSize(width: -event.scrollingDeltaX, height: -event.scrollingDeltaY), phase: .interacting)
        if event.momentumPhase == .ended || event.momentumPhase == .cancelled || event.phase == .cancelled {
            position.scroll(by: .zero, phase: .ended)
        } else if event.phase == .ended {
            // The fingers lifted. The system's momentum events may follow;
            // if none arrive, the gesture is over.
            let pending = generation
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.12) { [weak self, weak position] in
                guard let self, self.generation == pending else { return }
                position?.scroll(by: .zero, phase: .ended)
            }
        }
    }

    func stop() {
        if let token { NSEvent.removeMonitor(token) }
        token = nil
    }
}
#endif
