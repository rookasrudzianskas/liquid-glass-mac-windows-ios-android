import LiquidGlass
import SwiftUI

/// Every control over a wallpaper, in a native scroll view: the glass keeps
/// refracting the wallpaper at its exact position while the controls scroll.
struct ControlsPage: View {
    @State private var brightness = 0.6
    @State private var volume = 40.0
    @State private var wifi = true
    @State private var bluetooth = false
    @State private var airdrop = true
    @State private var range = Range.week
    @State private var sort = "Newest"
    @State private var likes = 0

    enum Range: Hashable { case day, week, month }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 28) {
                Text("Controls")
                    .font(.largeTitle.bold())
                    .foregroundStyle(.white)
                    .shadow(color: .black.opacity(0.2), radius: 6, y: 2)
                    .padding(.top, 8)
                buttons
                iconButtons
                toolbar
                values
                tabs
                menus
            }
            .padding(.horizontal, StudioLayout.pagePadding)
            .padding(.bottom, StudioLayout.tabBarClearance + 16)
            .frame(maxWidth: 620)
            .frame(maxWidth: .infinity)
        }
        .scrollIndicators(.hidden)
    }

    private var buttons: some View {
        VStack(alignment: .leading, spacing: 12) {
            SectionHeader(title: "Buttons")
            HStack(spacing: 10) {
                GlassButton("Small") {}.controlSize(.small)
                GlassButton("Regular") {}
                GlassButton("Large") {}.controlSize(.large)
            }
            HStack(spacing: 10) {
                GlassButton("Prominent", systemImage: "star.fill", prominent: true) {}
                GlassButton("Tinted", tint: Color(red: 1, green: 0.176, blue: 0.333)) {}
            }
            HStack(spacing: 10) {
                GlassButton("Regular glass", systemImage: "circle.hexagongrid", material: .regular) {}
                GlassButton("Disabled") {}.disabled(true)
            }
        }
    }

    private var iconButtons: some View {
        VStack(alignment: .leading, spacing: 12) {
            SectionHeader(title: "Icon buttons")
            HStack(spacing: 12) {
                GlassButton(icon: "play.fill", label: "Play") {}.controlSize(.small)
                GlassButton(icon: "heart.fill", label: "Like") { likes += 1 }
                GlassButton(icon: "bookmark", label: "Save") {}
                GlassButton(icon: "plus", label: "Add", prominent: true) {}
                GlassButton(icon: "paperplane.fill", label: "Send", tint: Color(red: 0.204, green: 0.78, blue: 0.349)) {}
                    .controlSize(.large)
            }
        }
    }

    private var toolbar: some View {
        VStack(alignment: .leading, spacing: 12) {
            SectionHeader(title: "Toolbar")
            GlassToolbar {
                GlassButton(icon: "chevron.left", label: "Back") {}
                GlassToolbarSpacer()
                GlassButton(icon: "bold", label: "Bold") {}
                GlassButton(icon: "italic", label: "Italic") {}
                GlassButton(icon: "underline", label: "Underline") {}
                GlassToolbarSpacer(.fixed)
                GlassButton(icon: "square.and.arrow.up", label: "Share") {}
            }
        }
    }

    private var values: some View {
        VStack(alignment: .leading, spacing: 12) {
            SectionHeader(title: "Slider and switch")
            GlassSurface(material: .regular, radius: 26) {
                VStack(spacing: 14) {
                    HStack(spacing: 12) {
                        Image(systemName: "sun.min")
                        GlassSlider(value: $brightness) { Text("Brightness") }
                        Image(systemName: "sun.max.fill")
                    }
                    HStack(spacing: 12) {
                        Image(systemName: "speaker.fill")
                        GlassSlider(value: $volume, in: 0...100, step: 10, tint: .orange) { Text("Volume") }
                        Image(systemName: "speaker.wave.3.fill")
                    }
                    Divider().opacity(0.6)
                    GlassSwitch("Wi-Fi", isOn: $wifi)
                    GlassSwitch("Bluetooth", isOn: $bluetooth)
                    GlassSwitch("AirDrop", isOn: $airdrop, tint: Color(red: 0.204, green: 0.78, blue: 0.349))
                }
                .font(.body)
                .padding(20)
            }
        }
    }

    private var tabs: some View {
        VStack(alignment: .leading, spacing: 12) {
            SectionHeader(title: "Tab bar")
            GlassTabBar(selection: $range) {
                GlassTab("Day", systemImage: "sun.max", value: Range.day)
                GlassTab("Week", systemImage: "calendar", value: Range.week)
                GlassTab("Month", systemImage: "square.grid.3x3", value: Range.month)
            }
            #if os(macOS)
            .fixedSize()
            #endif
        }
    }

    private var menus: some View {
        VStack(alignment: .leading, spacing: 12) {
            SectionHeader(title: "Menu")
            HStack(spacing: 12) {
                GlassMenu("Sort: \(sort)", systemImage: "arrow.up.arrow.down") {
                    GlassMenuItem("Newest", systemImage: "clock") { sort = "Newest" }
                    GlassMenuItem("Oldest", systemImage: "clock.arrow.circlepath") { sort = "Oldest" }
                    GlassMenuItem("Most liked", systemImage: "heart") { sort = "Most liked" }
                }
                GlassMenu(icon: "ellipsis", label: "More") {
                    GlassMenuItem("Duplicate", systemImage: "plus.square.on.square") {}
                    GlassMenuItem("Rename", systemImage: "pencil") {}
                    GlassMenuDivider()
                    GlassMenuItem("Delete", systemImage: "trash", role: .destructive) {}
                }
            }
        }
    }
}
