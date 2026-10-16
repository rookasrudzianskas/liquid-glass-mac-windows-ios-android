// swift-tools-version:5.9
//
// Liquid Glass for SwiftUI (iOS 17+, macOS 14+).
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
    ]
)
