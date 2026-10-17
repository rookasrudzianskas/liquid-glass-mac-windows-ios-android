// swift-tools-version:6.0
//
// Liquid Glass for SwiftUI (iOS 17+, macOS 14+).
//
// Builds with Xcode 16 or later: the iOS target uses iOS 18 SDK API behind
// availability checks, so the deployment targets stay iOS 17 and macOS 14.
// The sources use the Swift 5 language mode.
//
// The Metal shaders in Sources/LiquidGlass/Shaders are compiled by Xcode
// (xcodebuild or the Xcode app). `swift build` alone does not compile Metal,
// so build and test with xcodebuild; see docs/apple.md.

import PackageDescription

let package = Package(
    name: "LiquidGlass",
    platforms: [.iOS(.v17), .macOS(.v14)],
    products: [
        .library(name: "LiquidGlass", targets: ["LiquidGlass"]),
    ],
    targets: [
        .target(
            name: "LiquidGlass",
            path: "Sources/LiquidGlass",
            // Included by LiquidGlass.metal and by the GPU tests' kernels.
            exclude: ["Shaders/LiquidGlassCore.h"]
        ),
        .testTarget(
            name: "LiquidGlassTests",
            dependencies: ["LiquidGlass"],
            path: "Tests/LiquidGlassTests"
        ),
    ],
    swiftLanguageModes: [.v5]
)
