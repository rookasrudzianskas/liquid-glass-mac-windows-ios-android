import SwiftUI

@main
struct HelloGlassApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView()
        }
        #if os(macOS)
        // The wallpaper runs under the title bar; the toolbar stays below it.
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: 900, height: 680)
        #endif
    }
}
