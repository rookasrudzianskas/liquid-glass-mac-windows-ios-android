// Generates the shared Liquid Glass Engineering wallpapers.
//
//   swift assets/wallpapers/generate.swift
//
// Each wallpaper is layered ribbons of color with crisp, lit edges over soft
// glowing orbs: crisp edges are what make refraction visible. Output is
// square so every app can aspect-fill it, at 3072, 2048 and 1024 pixels.
// Deterministic: the same seed always draws the same image.

import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

struct RGB { var r: CGFloat, g: CGFloat, b: CGFloat }

func hex(_ value: UInt32) -> RGB {
    RGB(r: CGFloat((value >> 16) & 0xFF) / 255, g: CGFloat((value >> 8) & 0xFF) / 255, b: CGFloat(value & 0xFF) / 255)
}

func color(_ c: RGB, _ alpha: CGFloat = 1) -> CGColor {
    CGColor(srgbRed: c.r, green: c.g, blue: c.b, alpha: alpha)
}

struct Ribbon {
    var base: CGFloat          // vertical position of the ribbon's top edge, 0...1 from top
    var amplitude: CGFloat     // wave height, fraction of size
    var frequency: CGFloat     // waves across the width
    var phase: CGFloat
    var tilt: CGFloat          // rise across the width, fraction of size
    var from: RGB
    var to: RGB
}

struct Orb { var x: CGFloat, y: CGFloat, radius: CGFloat, color: RGB, alpha: CGFloat }

struct Wallpaper {
    var name: String
    var top: RGB
    var bottom: RGB
    var orbs: [Orb]
    var ribbons: [Ribbon]
    var edgeLight: CGFloat     // brightness of each ribbon's lit edge
    var shadow: CGFloat        // depth shadow between ribbons
}

let wallpapers: [Wallpaper] = [
    Wallpaper(
        name: "aurora", top: hex(0x070B24), bottom: hex(0x1A2466),
        orbs: [
            Orb(x: 0.22, y: 0.14, radius: 0.40, color: hex(0x2EE6C5), alpha: 0.60),
            Orb(x: 0.82, y: 0.22, radius: 0.40, color: hex(0x8A5CFF), alpha: 0.65),
            Orb(x: 0.50, y: 0.60, radius: 0.36, color: hex(0xFF5EA8), alpha: 0.35),
        ],
        ribbons: [
            Ribbon(base: 0.20, amplitude: 0.045, frequency: 1.10, phase: 5.2, tilt: 0.07, from: hex(0x7FF5E0), to: hex(0x48B7FF)),
            Ribbon(base: 0.33, amplitude: 0.050, frequency: 1.15, phase: 0.4, tilt: -0.06, from: hex(0x14D8C4), to: hex(0x3D7BFF)),
            Ribbon(base: 0.46, amplitude: 0.060, frequency: 0.90, phase: 2.1, tilt: 0.08, from: hex(0x5B4BFF), to: hex(0xC04BFF)),
            Ribbon(base: 0.59, amplitude: 0.045, frequency: 1.30, phase: 4.0, tilt: -0.04, from: hex(0xFF4FA3), to: hex(0x8B3DFF)),
            Ribbon(base: 0.72, amplitude: 0.055, frequency: 1.05, phase: 1.2, tilt: 0.05, from: hex(0x2A1E8F), to: hex(0x11C6D9)),
            Ribbon(base: 0.85, amplitude: 0.040, frequency: 1.40, phase: 3.3, tilt: -0.03, from: hex(0x0E1550), to: hex(0x3A1C8C)),
        ],
        edgeLight: 0.60, shadow: 0.45),
    Wallpaper(
        name: "dunes", top: hex(0xFFE0A8), bottom: hex(0xFF8A7A),
        orbs: [
            Orb(x: 0.72, y: 0.10, radius: 0.28, color: hex(0xFFF6D2), alpha: 0.90),
            Orb(x: 0.18, y: 0.40, radius: 0.45, color: hex(0xFF9E6B), alpha: 0.45),
        ],
        ribbons: [
            Ribbon(base: 0.21, amplitude: 0.040, frequency: 0.95, phase: 3.6, tilt: -0.05, from: hex(0xFFD08A), to: hex(0xFFA26B)),
            Ribbon(base: 0.34, amplitude: 0.045, frequency: 0.85, phase: 0.9, tilt: 0.07, from: hex(0xFFB25E), to: hex(0xFF6F61)),
            Ribbon(base: 0.47, amplitude: 0.055, frequency: 1.10, phase: 2.6, tilt: -0.05, from: hex(0xFF5F6D), to: hex(0xE0407B)),
            Ribbon(base: 0.60, amplitude: 0.050, frequency: 0.95, phase: 4.4, tilt: 0.04, from: hex(0xC2378F), to: hex(0x8E2DA8)),
            Ribbon(base: 0.73, amplitude: 0.045, frequency: 1.25, phase: 1.7, tilt: -0.06, from: hex(0x6A2A9E), to: hex(0x40238A)),
            Ribbon(base: 0.86, amplitude: 0.035, frequency: 1.35, phase: 5.1, tilt: 0.03, from: hex(0x2C1A66), to: hex(0x1B1248)),
        ],
        edgeLight: 0.70, shadow: 0.32),
    Wallpaper(
        name: "ocean", top: hex(0xCFF2FF), bottom: hex(0x4FA8FF),
        orbs: [
            Orb(x: 0.26, y: 0.10, radius: 0.32, color: hex(0xFFFFFF), alpha: 0.80),
            Orb(x: 0.82, y: 0.34, radius: 0.38, color: hex(0x7CF0FF), alpha: 0.45),
        ],
        ribbons: [
            Ribbon(base: 0.20, amplitude: 0.045, frequency: 1.05, phase: 4.1, tilt: 0.06, from: hex(0xA6F1FF), to: hex(0x69C6FF)),
            Ribbon(base: 0.33, amplitude: 0.050, frequency: 1.00, phase: 1.5, tilt: -0.07, from: hex(0x5EDCF5), to: hex(0x2E9BFF)),
            Ribbon(base: 0.46, amplitude: 0.060, frequency: 0.80, phase: 3.0, tilt: 0.06, from: hex(0x1FB6E0), to: hex(0x1866E8)),
            Ribbon(base: 0.59, amplitude: 0.045, frequency: 1.20, phase: 0.2, tilt: -0.05, from: hex(0x0D8BD1), to: hex(0x1C3FBF)),
            Ribbon(base: 0.72, amplitude: 0.055, frequency: 1.05, phase: 4.7, tilt: 0.04, from: hex(0x0B5FB0), to: hex(0x14238F)),
            Ribbon(base: 0.85, amplitude: 0.040, frequency: 1.30, phase: 2.2, tilt: -0.03, from: hex(0x08246E), to: hex(0x050F45)),
        ],
        edgeLight: 0.75, shadow: 0.30),
    Wallpaper(
        name: "midnight", top: hex(0x03040B), bottom: hex(0x0D1026),
        orbs: [
            Orb(x: 0.76, y: 0.16, radius: 0.36, color: hex(0x00D2FF), alpha: 0.45),
            Orb(x: 0.20, y: 0.52, radius: 0.40, color: hex(0xFF2E93), alpha: 0.32),
        ],
        ribbons: [
            Ribbon(base: 0.22, amplitude: 0.045, frequency: 1.15, phase: 1.1, tilt: -0.05, from: hex(0x1B2A6B), to: hex(0x2E1A6E)),
            Ribbon(base: 0.36, amplitude: 0.055, frequency: 0.95, phase: 2.8, tilt: 0.06, from: hex(0x00C6FF), to: hex(0x6C5CE7)),
            Ribbon(base: 0.49, amplitude: 0.050, frequency: 1.20, phase: 0.6, tilt: -0.06, from: hex(0x7B2FF7), to: hex(0xFF2E93)),
            Ribbon(base: 0.62, amplitude: 0.060, frequency: 0.85, phase: 3.9, tilt: 0.05, from: hex(0x2A1B5E), to: hex(0x14127A)),
            Ribbon(base: 0.75, amplitude: 0.045, frequency: 1.30, phase: 5.3, tilt: -0.04, from: hex(0x161A3A), to: hex(0x0B0F2C)),
            Ribbon(base: 0.87, amplitude: 0.035, frequency: 1.45, phase: 2.4, tilt: 0.03, from: hex(0x090B1C), to: hex(0x111430)),
        ],
        edgeLight: 0.55, shadow: 0.55),
]

/// Small deterministic generator for grain.
struct SplitMix64 {
    var state: UInt64
    mutating func next() -> UInt64 {
        state &+= 0x9E37_79B9_7F4A_7C15
        var z = state
        z = (z ^ (z >> 30)) &* 0xBF58_476D_1CE4_E5B9
        z = (z ^ (z >> 27)) &* 0x94D0_49BB_1331_11EB
        return z ^ (z >> 31)
    }
}

func ribbonPath(_ ribbon: Ribbon, size: CGFloat) -> CGMutablePath {
    let path = CGMutablePath()
    let steps = 256
    for i in 0...steps {
        let u = CGFloat(i) / CGFloat(steps)
        let wave = sin(u * .pi * 2 * ribbon.frequency + ribbon.phase)
            + 0.35 * sin(u * .pi * 2 * ribbon.frequency * 2.3 + ribbon.phase * 1.7)
        let y = (ribbon.base + ribbon.tilt * (u - 0.5) + ribbon.amplitude * wave / 1.35) * size
        let point = CGPoint(x: u * size, y: y)
        if i == 0 { path.move(to: point) } else { path.addLine(to: point) }
    }
    path.addLine(to: CGPoint(x: size, y: size))
    path.addLine(to: CGPoint(x: 0, y: size))
    path.closeSubpath()
    return path
}

func topEdge(_ ribbon: Ribbon, size: CGFloat) -> CGMutablePath {
    let path = CGMutablePath()
    let steps = 256
    for i in 0...steps {
        let u = CGFloat(i) / CGFloat(steps)
        let wave = sin(u * .pi * 2 * ribbon.frequency + ribbon.phase)
            + 0.35 * sin(u * .pi * 2 * ribbon.frequency * 2.3 + ribbon.phase * 1.7)
        let y = (ribbon.base + ribbon.tilt * (u - 0.5) + ribbon.amplitude * wave / 1.35) * size
        let point = CGPoint(x: u * size, y: y)
        if i == 0 { path.move(to: point) } else { path.addLine(to: point) }
    }
    return path
}

func render(_ wallpaper: Wallpaper, size pixels: Int) -> CGImage {
    let size = CGFloat(pixels)
    let space = CGColorSpace(name: CGColorSpace.sRGB)!
    let context = CGContext(data: nil, width: pixels, height: pixels, bitsPerComponent: 8, bytesPerRow: 0,
                            space: space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    // Draw in y-down coordinates.
    context.translateBy(x: 0, y: size)
    context.scaleBy(x: 1, y: -1)
    context.interpolationQuality = .high
    context.setShouldAntialias(true)

    let background = CGGradient(colorsSpace: space, colors: [color(wallpaper.top), color(wallpaper.bottom)] as CFArray, locations: [0, 1])!
    context.drawLinearGradient(background, start: .zero, end: CGPoint(x: 0, y: size), options: [])

    for orb in wallpaper.orbs {
        let center = CGPoint(x: orb.x * size, y: orb.y * size)
        let glow = CGGradient(colorsSpace: space, colors: [color(orb.color, orb.alpha), color(orb.color, orb.alpha * 0.35), color(orb.color, 0)] as CFArray, locations: [0, 0.45, 1])!
        context.drawRadialGradient(glow, startCenter: center, startRadius: 0, endCenter: center, endRadius: orb.radius * size, options: [])
    }

    for ribbon in wallpaper.ribbons {
        let path = ribbonPath(ribbon, size: size)
        // Depth: a soft shadow cast upward onto the ribbon behind.
        context.saveGState()
        context.setShadow(offset: CGSize(width: 0, height: size * 0.006), blur: size * 0.035, color: CGColor(srgbRed: 0, green: 0, blue: 0.05, alpha: wallpaper.shadow))
        context.addPath(path)
        context.setFillColor(color(ribbon.from))
        context.fillPath()
        context.restoreGState()

        // Body: a diagonal gradient across the ribbon.
        context.saveGState()
        context.addPath(path)
        context.clip()
        let body = CGGradient(colorsSpace: space, colors: [color(ribbon.from), color(ribbon.to)] as CFArray, locations: [0, 1])!
        context.drawLinearGradient(body, start: CGPoint(x: 0, y: ribbon.base * size), end: CGPoint(x: size, y: (ribbon.base + 0.25) * size), options: [.drawsBeforeStartLocation, .drawsAfterEndLocation])
        // Inner shading: darker deeper into the ribbon.
        let shade = CGGradient(colorsSpace: space, colors: [CGColor(srgbRed: 1, green: 1, blue: 1, alpha: 0.10), CGColor(srgbRed: 0, green: 0, blue: 0.04, alpha: 0.28)] as CFArray, locations: [0, 1])!
        let crest = ribbon.base - ribbon.amplitude - abs(ribbon.tilt) / 2
        context.drawLinearGradient(shade, start: CGPoint(x: 0, y: crest * size), end: CGPoint(x: 0, y: (crest + 0.24) * size), options: [.drawsBeforeStartLocation, .drawsAfterEndLocation])
        context.restoreGState()

        // Lit edge: a crisp highlight along the top, with a soft bloom.
        let edge = topEdge(ribbon, size: size)
        context.saveGState()
        context.addPath(edge)
        context.setLineCap(.round)
        context.setLineJoin(.round)
        context.setShadow(offset: .zero, blur: size * 0.008, color: CGColor(srgbRed: 1, green: 1, blue: 1, alpha: wallpaper.edgeLight * 0.8))
        context.setStrokeColor(CGColor(srgbRed: 1, green: 1, blue: 1, alpha: wallpaper.edgeLight))
        context.setLineWidth(max(1, size * 0.0014))
        context.strokePath()
        context.restoreGState()
    }

    // Vignette.
    let vignette = CGGradient(colorsSpace: space, colors: [CGColor(gray: 0, alpha: 0), CGColor(gray: 0, alpha: 0.22)] as CFArray, locations: [0.55, 1])!
    context.drawRadialGradient(vignette, startCenter: CGPoint(x: size / 2, y: size / 2), startRadius: 0, endCenter: CGPoint(x: size / 2, y: size / 2), endRadius: size * 0.75, options: [.drawsAfterEndLocation])

    // Fine grain keeps gradients from banding.
    let image = context.makeImage()!
    guard let data = context.data else { return image }
    let bytes = data.bindMemory(to: UInt8.self, capacity: context.bytesPerRow * pixels)
    var random = SplitMix64(state: wallpaper.name.unicodeScalars.reduce(7) { $0 &* 31 &+ UInt64($1.value) })
    for y in 0..<pixels {
        let row = bytes + y * context.bytesPerRow
        for x in 0..<pixels {
            let noise = Int(random.next() % 9) - 4
            for c in 0..<3 {
                let value = Int(row[x * 4 + c]) + noise
                row[x * 4 + c] = UInt8(max(0, min(255, value)))
            }
        }
    }
    return context.makeImage()!
}

func write(_ image: CGImage, to url: URL, quality: CGFloat) {
    let destination = CGImageDestinationCreateWithURL(url as CFURL, UTType.jpeg.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(destination, image, [kCGImageDestinationLossyCompressionQuality: quality] as CFDictionary)
    precondition(CGImageDestinationFinalize(destination), "Could not write \(url.path)")
}

let directory = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
for wallpaper in wallpapers {
    for pixels in [3072, 2048, 1024] {
        let image = render(wallpaper, size: pixels)
        let url = directory.appendingPathComponent("\(wallpaper.name)-\(pixels).jpg")
        write(image, to: url, quality: pixels >= 2048 ? 0.86 : 0.9)
        print("wrote \(url.lastPathComponent)")
    }
}
