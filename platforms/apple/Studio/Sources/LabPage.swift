import LiquidGlass
import SwiftUI

/// A draggable lens with live controls for material, appearance,
/// refraction, radius, tint and the wallpaper beneath it.
struct LabPage: View {
    @Bindable var settings: LabSettings

    @State private var lensOffset = CGSize.zero
    @State private var dragStart = CGSize.zero

    var body: some View {
        GeometryReader { proxy in
            let wide = proxy.size.width > 720
            let layout = wide ? AnyLayout(HStackLayout(alignment: .bottom, spacing: 24)) : AnyLayout(VStackLayout(spacing: 16))
            layout {
                lensArea
                panel
                    .frame(width: wide ? 340 : nil)
                    // Room for the lens above a tall panel (the largest text
                    // sizes); the panel scrolls within what is left.
                    .frame(maxHeight: wide ? nil : max(240, proxy.size.height - Self.lensRoom))
                    // The panel is measured first, so it shows every control
                    // whenever they fit and the lens takes the rest (the
                    // stack would otherwise split the height evenly and make
                    // the panel scroll, hiding its last rows).
                    .layoutPriority(1)
            }
            .padding(.horizontal, StudioLayout.pagePadding)
            .padding(.top, 12)
            .padding(.bottom, StudioLayout.tabBarClearance)
        }
    }

    /// The height kept for the lens when the panel is under it.
    private static let lensRoom: CGFloat = 250

    private var lensArea: some View {
        GlassSurface(
            material: settings.material, appearance: settings.appearance, radius: settings.glassRadius,
            refraction: settings.refraction, tint: settings.tint.color, interactive: true
        ) {
            VStack(spacing: 6) {
                Image(systemName: "drop.fill")
                    .font(.title.weight(.semibold))
                Text("Drag me")
                    .font(.headline)
            }
            .frame(width: 210, height: 210)
        }
        .offset(lensOffset)
        .gesture(
            DragGesture()
                .onChanged { lensOffset = CGSize(width: dragStart.width + $0.translation.width, height: dragStart.height + $0.translation.height) }
                .onEnded { _ in dragStart = lensOffset }
        )
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .accessibilityLabel("Lens")
    }

    private var panel: some View {
        GlassSurface(material: .regular, radius: 28) {
            ViewThatFits(in: .vertical) {
                panelControls
                ScrollView { panelControls }
                    .scrollIndicators(.hidden)
            }
        }
    }

    private var panelControls: some View {
        // The pickers are Regular glass like the panel they sit on (glass
        // doesn't refract other glass, so Clear pickers would show the sharp
        // wallpaper through the frosted panel).
        VStack(alignment: .leading, spacing: 14) {
            GlassTabBar(selection: $settings.material, material: .regular) {
                GlassTab("Clear", value: GlassMaterial.clear)
                GlassTab("Regular", value: GlassMaterial.regular)
            }
            GlassTabBar(selection: $settings.appearance, material: .regular) {
                GlassTab("Auto", value: GlassAppearance.automatic)
                GlassTab("Light", value: GlassAppearance.light)
                GlassTab("Dark", value: GlassAppearance.dark)
            }
            LabeledSlider(title: "Refraction", value: $settings.refraction, range: 0...120, format: "%.0f")
            HStack(spacing: 12) {
                LabeledSlider(title: "Radius", value: $settings.radius, range: 0...110, format: "%.0f")
                    .disabled(settings.capsule)
                GlassSwitch("Capsule", isOn: $settings.capsule)
                    .fixedSize()
            }
            swatches
            wallpapers
        }
        .controlSize(.small)
        .padding(18)
    }

    private var swatches: some View {
        HStack(spacing: 10) {
            ForEach(LabTint.allCases) { tint in
                Button {
                    settings.tint = tint
                } label: {
                    Circle()
                        .fill(tint.color ?? Color.white.opacity(0.25))
                        .overlay {
                            if tint == .none {
                                Image(systemName: "slash.circle").font(.system(size: 16, weight: .medium))
                            }
                        }
                        .frame(width: 28, height: 28)
                        .padding(3)
                        .overlay(Circle().strokeBorder(.foreground.opacity(settings.tint == tint ? 0.8 : 0), lineWidth: 2))
                }
                .buttonStyle(.plain)
                .accessibilityLabel(tint == .none ? "No tint" : "\(tint.rawValue.capitalized) tint")
                .accessibilityAddTraits(settings.tint == tint ? .isSelected : [])
            }
        }
    }

    private var wallpapers: some View {
        HStack(spacing: 10) {
            ForEach(Wallpaper.allCases) { wallpaper in
                Button {
                    settings.wallpaper = wallpaper
                } label: {
                    Image(wallpaper.rawValue)
                        .resizable()
                        .scaledToFill()
                        .frame(width: 52, height: 36)
                        .clipShape(GlassShape(radius: 9))
                        .padding(3)
                        .overlay(GlassShape(radius: 12).strokeBorder(.foreground.opacity(settings.wallpaper == wallpaper ? 0.8 : 0), lineWidth: 2))
                }
                .buttonStyle(.plain)
                .accessibilityLabel("\(wallpaper.title) wallpaper")
                .accessibilityAddTraits(settings.wallpaper == wallpaper ? .isSelected : [])
            }
        }
    }
}

/// A slider with its title and value.
private struct LabeledSlider: View {
    let title: String
    @Binding var value: Double
    let range: ClosedRange<Double>
    let format: String

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack {
                Text(title).font(.subheadline.weight(.medium))
                Spacer()
                Text(String(format: format, value)).font(.subheadline.monospacedDigit()).opacity(0.7)
            }
            GlassSlider(value: $value, in: range) { Text(title) }
        }
    }
}
