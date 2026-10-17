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
/// ends; trackpad, mouse wheel and keyboard (arrows, Page Up and Down, Space,
/// Home and End once it has focus) on macOS. Its ``GlassScrollPosition`` is
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
    @Environment(\.layoutDirection) private var layoutDirection

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
                .modifier(ScrollInput(
                    position: position, enabled: interactive, frame: viewport.frame(in: .global),
                    rightToLeft: layoutDirection == .rightToLeft
                ))
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

/// Touch, scroll-wheel and keyboard input for the visible scroll view
/// (copies inside glass only display the shared position).
///
/// Right to left, content starts at the right edge and SwiftUI mirrors the
/// offset, so horizontal input is mirrored too: the content follows the
/// finger.
private struct ScrollInput: ViewModifier {
    let position: GlassScrollPosition
    let enabled: Bool
    let frame: CGRect
    let rightToLeft: Bool

    @GestureState private var touching = false
    @GestureState private var dragging = false
    @State private var fingerDown = false

    private var horizontal: CGFloat { rightToLeft ? -1 : 1 }

    func body(content: Content) -> some View {
        if enabled {
            content
                .contentShape(Rectangle())
                #if os(macOS)
                .background(ScrollWheelMonitor(position: position, frame: frame, horizontal: horizontal))
                .modifier(ScrollKeys(position: position))
                #else
                // A finger landing stops the coasting content where it is.
                .modifier(GlassTouchInput(
                    space: LocalCoordinateSpace.local, tracking: $touching,
                    changed: { _ in
                        if !fingerDown {
                            fingerDown = true
                            position.touchDown()
                        }
                    },
                    changedGlobally: { _ in },
                    ended: { _ in
                        fingerDown = false
                        position.touchUp()
                    }
                ))
                .gesture(
                    DragGesture(minimumDistance: 8)
                        .updating($dragging) { _, state, _ in state = true }
                        .onChanged { position.drag(translation: CGSize(width: $0.translation.width * horizontal, height: $0.translation.height)) }
                        .onEnded { position.endDrag(velocity: CGSize(width: $0.velocity.width * horizontal, height: $0.velocity.height)) }
                )
                // SwiftUI doesn't call `onEnded` for a cancelled drag (the
                // system took the touch); end it here so the content settles
                // and the display link can stop. A completed drag has already
                // ended, and ending it again does nothing.
                .onChange(of: dragging) { _, isDragging in
                    if !isDragging { position.endDrag(velocity: .zero) }
                }
                #endif
        } else {
            content.allowsHitTesting(false)
        }
    }
}

#if os(macOS)
/// Arrow keys, Page Up and Down, Space (Shift-Space up), Home and End.
private struct ScrollKeys: ViewModifier {
    let position: GlassScrollPosition

    func body(content: Content) -> some View {
        content
            .focusable()
            .focusEffectDisabled()
            .onKeyPress(keys: [.upArrow, .downArrow, .leftArrow, .rightArrow, .pageUp, .pageDown, .space, .home, .end]) { press in
                let viewport = position.viewportSize
                let line: CGFloat = 40
                let page = max(line, viewport.height * 0.9)
                let delta: CGSize
                switch press.key {
                case .upArrow: delta = CGSize(width: 0, height: -line)
                case .downArrow: delta = CGSize(width: 0, height: line)
                case .leftArrow: delta = CGSize(width: -line, height: 0)
                case .rightArrow: delta = CGSize(width: line, height: 0)
                case .pageUp: delta = CGSize(width: 0, height: -page)
                case .pageDown: delta = CGSize(width: 0, height: page)
                case .space: delta = CGSize(width: 0, height: press.modifiers.contains(.shift) ? -page : page)
                case .home:
                    position.scrollTo(edge: .top)
                    return .handled
                case .end:
                    position.scrollTo(edge: .bottom)
                    return .handled
                default: return .ignored
                }
                position.scroll(byKeyboard: delta)
                return .handled
            }
    }
}

/// Routes the window's scroll-wheel events over `frame` to the position.
/// Trackpads deliver precise deltas and their own momentum; mouse wheels
/// deliver lines.
private struct ScrollWheelMonitor: View {
    let position: GlassScrollPosition
    let frame: CGRect
    let horizontal: CGFloat

    @State private var monitor = WheelMonitor()

    var body: some View {
        WindowAnchor { view in monitor.anchor = view }
            .onAppear { monitor.start(position: position) }
            .onDisappear { monitor.stop() }
            .onChange(of: frame, initial: true) { _, frame in monitor.frame = frame }
            .onChange(of: horizontal, initial: true) { _, horizontal in monitor.horizontal = horizontal }
    }
}

/// An empty AppKit view that tells the monitor which window, and where in
/// it, the scroll view lives.
private struct WindowAnchor: NSViewRepresentable {
    let found: (NSView) -> Void

    func makeNSView(context: Context) -> NSView {
        let view = NSView()
        DispatchQueue.main.async { found(view) }
        return view
    }

    func updateNSView(_ view: NSView, context: Context) {}
}

@MainActor
final class WheelMonitor {
    var frame = CGRect.zero
    var horizontal: CGFloat = 1
    weak var anchor: NSView?
    private var token: Any?
    /// Bumped by every event, so a pending end is dropped when more follow.
    private var generation = 0
    /// A trackpad gesture (or its momentum) that began over the scroll view.
    /// AppKit keeps sending its events to the same window wherever the
    /// pointer goes, and they still belong to this scroll view.
    private var gestureActive = false

    func start(position: GlassScrollPosition) {
        guard token == nil else { return }
        token = NSEvent.addLocalMonitorForEvents(matching: .scrollWheel) { [weak self, weak position] event in
            guard let self, let position, self.accepts(event) else { return event }
            self.handle(event, position: position)
            return nil
        }
    }

    /// Only events for this scroll view's window, when no sheet covers it,
    /// over the scroll view and not over an AppKit view in front of it (a
    /// native scroll view or text view takes its own wheel events).
    private func accepts(_ event: NSEvent) -> Bool {
        guard let anchor, let window = anchor.window, event.window === window,
              window.attachedSheet == nil, let content = window.contentView else { return false }
        if continuesGesture(event) { return true }
        var point = content.convert(event.locationInWindow, from: nil)
        if !content.isFlipped { point.y = content.bounds.height - point.y }
        guard frame.contains(point) else { return false }
        // SwiftUI views share one hosting view; an AppKit scroll view (a
        // native ScrollView, List or TextEditor) in front scrolls itself.
        if let frameView = content.superview,
           let hit = frameView.hitTest(frameView.convert(event.locationInWindow, from: nil)),
           hit is NSScrollView || hit.enclosingScrollView != nil {
            return false
        }
        return true
    }

    /// Whether `event` continues the trackpad gesture (or its momentum)
    /// under way, which belongs to this scroll view wherever the pointer is
    /// by now.
    func continuesGesture(_ event: NSEvent) -> Bool {
        gestureActive && event.phase != .began && event.phase != .mayBegin
    }

    func handle(_ event: NSEvent, position: GlassScrollPosition) {
        generation += 1
        guard event.hasPreciseScrollingDeltas else {
            let line: CGFloat = 16
            position.scroll(by: CGSize(width: -event.scrollingDeltaX * line * horizontal, height: -event.scrollingDeltaY * line), phase: .step)
            return
        }
        if event.phase == .began || event.phase == .mayBegin { gestureActive = true }
        position.scroll(by: CGSize(width: -event.scrollingDeltaX * horizontal, height: -event.scrollingDeltaY), phase: .interacting)
        if event.momentumPhase == .ended || event.momentumPhase == .cancelled || event.phase == .cancelled {
            end(position)
        } else if event.phase == .ended || !event.momentumPhase.isEmpty {
            // The fingers lifted, or momentum is coasting. More momentum
            // events may follow; if none arrive, the gesture is over.
            let pending = generation
            DispatchQueue.main.asyncAfter(deadline: .now() + (event.momentumPhase.isEmpty ? 0.12 : 0.3)) { [weak self, weak position] in
                guard let self, let position, self.generation == pending else { return }
                self.end(position)
            }
        }
    }

    private func end(_ position: GlassScrollPosition) {
        gestureActive = false
        position.scroll(by: .zero, phase: .ended)
    }

    func stop() {
        if let token { NSEvent.removeMonitor(token) }
        token = nil
        gestureActive = false
    }
}
#endif
