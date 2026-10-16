import LiquidGlass
import SwiftUI

@main
struct LiquidGlassStudioApp: App {
    var body: some Scene {
        WindowGroup {
            StudioRoot()
        }
    }
}

struct StudioRoot: View {
    @State private var offset = CGSize.zero
    @State private var dragStart = CGSize.zero
    var body: some View {
        GlassScene {
            GlassContent {
                Image("aurora").resizable().scaledToFill()
            }
            .ignoresSafeArea()
            VStack(spacing: 24) {
                GlassSurface(material: .regular, radius: 32, interactive: true) {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Liquid Glass").font(.largeTitle.bold())
                        Text("Refraction, frost and light, natively.").font(.headline)
                    }
                    .padding(28)
                    .frame(width: 320, alignment: .leading)
                }
                .offset(offset)
                .gesture(DragGesture().onChanged { offset = CGSize(width: dragStart.width + $0.translation.width, height: dragStart.height + $0.translation.height) }.onEnded { _ in dragStart = offset })
                HStack(spacing: 16) {
                    GlassSurface(radius: .capsule, interactive: true) { Text("Clear").font(.headline).padding(.horizontal, 24).frame(height: 52) }
                    GlassSurface(material: .regular, radius: .capsule, interactive: true) { Text("Regular").font(.headline).padding(.horizontal, 24).frame(height: 52) }
                    GlassSurface(radius: .circle, interactive: true) { Image(systemName: "plus").font(.title2.weight(.semibold)).frame(width: 52, height: 52) }
                }
                GlassSurface(appearance: .dark, radius: 24) { Text("Dark clear glass").font(.headline).padding(24) }
                GlassSurface(radius: 24, tint: .blue) { Text("Tinted").font(.headline).foregroundStyle(.white).padding(24) }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
    }
}
