import SwiftUI

/// A popup a control presents above its scene's foreground: menus attach
/// here so their glass can cover everything else in the scene and still
/// refract the scene's backdrop.
struct GlassOverlayItem: Identifiable {
    let id: AnyHashable
    /// The bounds of the control that presented the popup.
    let anchor: Anchor<CGRect>
    /// Builds the popup from the presenter's frame and the scene's size, both
    /// in the scene's coordinate space.
    let content: (_ presenter: CGRect, _ scene: CGSize) -> AnyView
    /// Called for a tap outside the popup.
    let dismiss: () -> Void
}

struct GlassOverlayKey: PreferenceKey {
    static let defaultValue: [GlassOverlayItem] = []

    static func reduce(value: inout [GlassOverlayItem], nextValue: () -> [GlassOverlayItem]) {
        value.append(contentsOf: nextValue())
    }
}

/// Draws the scene's open popups, each over a transparent layer that
/// dismisses it when tapped.
struct GlassOverlayLayer: View {
    let items: [GlassOverlayItem]

    var body: some View {
        if !items.isEmpty {
            GeometryReader { proxy in
                ZStack(alignment: .topLeading) {
                    ForEach(items) { item in
                        Color.clear
                            .contentShape(Rectangle())
                            .onTapGesture(perform: item.dismiss)
                            .accessibilityHidden(true)
                        item.content(proxy[item.anchor], proxy.size)
                    }
                }
                .frame(width: proxy.size.width, height: proxy.size.height, alignment: .topLeading)
            }
        }
    }
}
