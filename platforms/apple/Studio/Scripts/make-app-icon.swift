// make-app-icon.swift
//
// Renders the Liquid Glass Studio app icon: an aurora gradient seen through a
// single refracting glass lens. Everything is procedural, so the lens samples
// the background function at displaced positions instead of warping pixels.
//
// Writes into an .appiconset directory:
//   - AppIcon-iOS.png / -Dark.png / -Tinted.png   1024 px, opaque, full bleed
//   - icon_<n>x<n>[@2x].png                        macOS Big Sur squircle set
//   - Contents.json                                Xcode 16 single-size format
//
// Usage (from the repo root):
//   DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
//     swift platforms/apple/Studio/Scripts/make-app-icon.swift [output.appiconset]
// Without an argument it writes to Studio/Resources/Assets.xcassets/AppIcon.appiconset.

import CoreGraphics
import Foundation
import ImageIO

// MARK: - Colour

struct RGB {
    var r, g, b: Double
    init(_ r: Double, _ g: Double, _ b: Double) { self.r = r; self.g = g; self.b = b }

    /// sRGB hex literal converted to linear light.
    init(hex: UInt32) {
        func lin(_ v: UInt32) -> Double {
            let c = Double(v & 0xFF) / 255
            return c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4)
        }
        self.init(lin(hex >> 16), lin(hex >> 8), lin(hex))
    }

    static let white = RGB(1, 1, 1)
    var luminance: Double { 0.2126 * r + 0.7152 * g + 0.0722 * b }

    static func + (a: RGB, b: RGB) -> RGB { RGB(a.r + b.r, a.g + b.g, a.b + b.b) }
    static func * (a: RGB, k: Double) -> RGB { RGB(a.r * k, a.g * k, a.b * k) }

    func mixed(with o: RGB, _ t: Double) -> RGB { self * (1 - t) + o * t }
    func saturated(_ k: Double) -> RGB { RGB(luminance, luminance, luminance).mixed(with: self, k) }
}

func clamp(_ x: Double, _ lo: Double = 0, _ hi: Double = 1) -> Double { min(max(x, lo), hi) }

func smoothstep(_ a: Double, _ b: Double, _ x: Double) -> Double {
    let t = clamp((x - a) / (b - a))
    return t * t * (3 - 2 * t)
}

/// Linear light to an 8-bit sRGB code value, with dither to hide banding.
func encode(_ c: Double, dither: Double) -> UInt8 {
    let v = clamp(c)
    let s = v <= 0.0031308 ? v * 12.92 : 1.055 * pow(v, 1 / 2.4) - 0.055
    return UInt8(clamp(s * 255 + dither, 0, 255).rounded())
}

/// Deterministic per-pixel hash in [0, 1).
func hash(_ x: Int, _ y: Int, _ seed: Int) -> Double {
    var h = UInt32(truncatingIfNeeded: x &* 374_761_393 &+ y &* 668_265_263 &+ seed &* 2_147_483_647)
    h = (h ^ (h >> 13)) &* 1_274_126_177
    h ^= h >> 16
    return Double(h) / 4_294_967_296
}

// MARK: - Scene

/// Per-variant controls: the iOS appearances, plus a bolder take for the
/// tiniest macOS sizes where hairline details would vanish.
struct Look {
    var backdrop: Double    // aurora gain outside the lens
    var throughLens: Double // aurora gain seen through the lens
    var shadow: Double      // opacity of the lens drop shadow
    var stroke: Double      // width multiplier for the rim's fine lines

    static let light = Look(backdrop: 1, throughLens: 1, shadow: 0.5, stroke: 1)
    static let dark = Look(backdrop: 0.24, throughLens: 0.85, shadow: 0.6, stroke: 1)
    static let tinted = Look(backdrop: 0.14, throughLens: 0.8, shadow: 0.6, stroke: 1)
    static let small = Look(backdrop: 0.85, throughLens: 1.05, shadow: 0.65, stroke: 5)
}

let indigoDeep = RGB(hex: 0x12063A)
let violet = RGB(hex: 0x3A1690)
let nightBlue = RGB(hex: 0x0B0A3C)
let royal = RGB(hex: 0x5A30FF)
let teal = RGB(hex: 0x2DF5C8)
let cyan = RGB(hex: 0x39C6FF)
let magenta = RGB(hex: 0xF23BC4)
let pink = RGB(hex: 0xFF6FA0)
let lilac = RGB(hex: 0x8A5CFF)
let causticTint = RGB(hex: 0xFF8AD8)

/// Aurora curtain: bright, fairly sharp lower edge that fades slowly upward.
func curtain(_ y: Double, edge: Double, below: Double, above: Double) -> Double {
    let d = y - edge
    return d >= 0 ? exp(-(d * d) / (below * below)) : exp(d / above)
}

/// The aurora backdrop. (x, y) are canvas units: 0...1, y pointing down.
/// Defined everywhere so the lens can sample outside the canvas.
func aurora(_ x: Double, _ y: Double, gain: Double) -> RGB {
    // Base: night blue at the top, violet through the middle, deep indigo below.
    let t = clamp(y + 0.06 * sin(2.3 * x + 0.4))
    var c = t < 0.55
        ? nightBlue.mixed(with: violet, smoothstep(0, 0.55, t))
        : violet.mixed(with: indigoDeep, smoothstep(0.55, 1, t))

    // Fine vertical rays give the curtains texture for the lens to magnify.
    let rays = 0.72 + 0.28 * sin(38 * x + 2.2 * sin(5 * x))

    // Teal to cyan curtain sweeping through the upper half.
    let e1 = 0.43 + 0.075 * sin(5.2 * x + 0.3) + 0.03 * sin(11.5 * x + 1.7) - 0.12 * (x - 0.5)
    let a1 = curtain(y, edge: e1, below: 0.022, above: 0.15) * rays
    let f1 = exp(-pow((y - e1 + 0.004) / 0.006, 2)) // bright filament on the lower edge
    let hue1 = teal.mixed(with: cyan, smoothstep(0.1, 0.9, x)).mixed(with: royal, 0.9 * smoothstep(0, 0.22, e1 - y))
    c = c + hue1 * (1.25 * a1) + cyan.mixed(with: .white, 0.3) * (0.5 * f1)

    // Magenta to pink wave lower down, flowing the other way.
    let e2 = 0.70 + 0.06 * sin(4.4 * x + 2.6) + 0.025 * sin(9.7 * x + 0.4) + 0.10 * (x - 0.5)
    let a2 = curtain(y, edge: e2, below: 0.05, above: 0.16) * (0.9 + 0.1 * rays)
    c = c + magenta.mixed(with: pink, smoothstep(0.2, 1, x)) * (1.1 * a2)

    // Soft lilac haze lifting the space between the two bands on the right.
    let gx = x - 0.85, gy = y - 0.55
    c = c + lilac * (0.3 * exp(-(gx * gx + gy * gy) / 0.05))

    // Soft shoulder so bright cores bloom toward white instead of clipping.
    c = c * gain
    let peak = max(c.r, c.g, c.b)
    if peak > 0.75 {
        let k = (0.75 + 0.25 * (1 - exp(-(peak - 0.75) / 0.25))) / peak
        c = c * k + RGB.white * (0.18 * (peak - 0.75))
    }
    return c
}

// MARK: - Lens

let lensX = 0.5, lensY = 0.465, lensR = 0.298
let light = (x: -0.42, y: -0.91) // key light direction (towards upper left)

/// Radial refraction profile in lens radii: the centre is gently magnified while
/// the curved bezel compresses the view and pulls in the aurora from outside.
/// `k` scales the bezel term per colour channel for a hint of dispersion.
func refractedRadius(_ u: Double, _ k: Double) -> Double { 0.8 * u + 0.42 * k * pow(u, 4) }

/// Final colour of the scene at canvas point (x, y). `px` is one pixel in canvas units.
func scene(_ x: Double, _ y: Double, px: Double, look: Look) -> RGB {
    let dx = x - lensX, dy = y - lensY
    let r = (dx * dx + dy * dy).squareRoot()
    let nx = r > 0 ? dx / r : 0, ny = r > 0 ? dy / r : 0
    let facing = nx * light.x + ny * light.y // +1 towards the light, -1 away
    let coverage = clamp(0.5 + (lensR - r) / px)
    let line = 0.005 * look.stroke

    var outside = RGB(0, 0, 0)
    if coverage < 1 {
        let backdrop = aurora(x, y, gain: look.backdrop)
        // Soft drop shadow away from the light, a tight contact shade, and the
        // caustic arc of light the lens focuses into its own shadow.
        let sx = dx - 0.01, sy = dy - 0.035
        let drop = 1 - smoothstep(-0.07, 0.1, (sx * sx + sy * sy).squareRoot() - 0.95 * lensR)
        let contact = exp(-max(r - lensR, 0) / (2.4 * line))
        let caustic = 0.4 * exp(-pow((r - lensR - 0.03) / 0.026, 2)) * pow(max(-facing, 0), 6)
        outside = backdrop * ((1 - look.shadow * drop) * (1 - 0.3 * contact))
            + causticTint * caustic
    }
    if coverage <= 0 { return outside }

    let u = min(r / lensR, 1)
    let e = max(lensR - r, 0) // depth inside the rim

    func refracted(_ k: Double) -> RGB {
        let sr = lensR * refractedRadius(u, k)
        return aurora(lensX + nx * sr, lensY + ny * sr, gain: look.throughLens)
    }
    var c = RGB(refracted(0.95).r, refracted(1).g, refracted(1.05).b)

    // Glass body: slightly brighter and more vivid, with a faint sheen up top.
    c = c.saturated(1.12) * 1.04 + RGB(0.004, 0.004, 0.01)
    c = c + RGB.white * (0.035 * smoothstep(0.2, -0.95, ny * u))

    // Thicker glass at the left and right edges reads slightly darker.
    let side = pow(abs(nx), 3) * exp(-e / (0.16 * lensR))
    c = c * (1 - 0.3 * side)

    // Light gathered in the curved bezel, and a fine Fresnel edge all round.
    c = c + RGB.white * (0.07 * exp(-e / (0.1 * lensR)) + (0.22 + 0.04 * look.stroke) * exp(-pow(e / line, 2)))

    // Rim light: crisp specular along the top, softer glow along the bottom.
    let top = pow(max(facing, 0), 1.3), bottom = pow(max(-facing, 0), 1.8)
    let soft = exp(-e / (0.07 * lensR))
    c = c + RGB.white * (1.4 * top * exp(-pow(e / (0.9 * line), 2)) + 0.35 * top * exp(-pow(e / (2.8 * line), 2))
        + 0.12 * top * top * soft)
    c = c + c.mixed(with: .white, 0.6) * (0.9 * bottom * exp(-pow(e / (1.8 * line), 2)) + 0.16 * bottom * soft)

    return outside.mixed(with: c, coverage)
}

// MARK: - Rasterising

let sRGB = CGColorSpace(name: CGColorSpace.sRGB)!

func bitmap(_ size: Int, opaque: Bool) -> CGContext {
    let alpha: CGImageAlphaInfo = opaque ? .noneSkipLast : .premultipliedLast
    let ctx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: size * 4,
                        space: sRGB, bitmapInfo: alpha.rawValue)!
    ctx.interpolationQuality = .high
    return ctx
}

/// Renders the full-bleed square artwork; `grayscale` keeps luminance only.
func renderArtwork(size: Int, look: Look, grayscale: Bool = false) -> CGImage {
    let ctx = bitmap(size, opaque: true)
    let pixels = ctx.data!.bindMemory(to: UInt8.self, capacity: size * size * 4)
    let px = 1 / Double(size)
    for j in 0..<size {
        for i in 0..<size {
            var c = scene((Double(i) + 0.5) * px, (Double(j) + 0.5) * px, px: px, look: look)
            if grayscale {
                let l = pow(c.luminance, 0.8)
                c = RGB(l, l, l)
            }
            let o = (j * size + i) * 4
            pixels[o] = encode(c.r, dither: hash(i, j, 1) + hash(i, j, 2) - 1)
            pixels[o + 1] = encode(c.g, dither: hash(i, j, 3) + hash(i, j, 4) - 1)
            pixels[o + 2] = encode(c.b, dither: hash(i, j, 5) + hash(i, j, 6) - 1)
            pixels[o + 3] = 255
        }
    }
    return ctx.makeImage()!
}

// MARK: - macOS

/// Continuous-corner rounded rect: straight edges joined by superellipse
/// quadrants (exponent ~3.26) spanning 1.5287 x radius, as in Apple's squircle.
func squircle(_ rect: CGRect, radius: CGFloat) -> CGPath {
    let span = min(radius * 1.528665, rect.width / 2, rect.height / 2)
    let n = 3.2586
    let path = CGMutablePath()
    let corners: [(cx: CGFloat, cy: CGFloat, sx: CGFloat, sy: CGFloat, from: Double)] = [
        (rect.maxX - span, rect.maxY - span, 1, 1, 0),
        (rect.minX + span, rect.maxY - span, -1, 1, .pi / 2),
        (rect.minX + span, rect.minY + span, -1, -1, .pi),
        (rect.maxX - span, rect.minY + span, 1, -1, 3 * .pi / 2),
    ]
    let steps = 64
    for (index, corner) in corners.enumerated() {
        for s in 0...steps {
            let a = corner.from + Double(s) / Double(steps) * .pi / 2
            let ux = pow(abs(cos(a)), 2 / n), uy = pow(abs(sin(a)), 2 / n)
            let p = CGPoint(x: corner.cx + corner.sx * span * ux, y: corner.cy + corner.sy * span * uy)
            if index == 0 && s == 0 { path.move(to: p) } else { path.addLine(to: p) }
        }
    }
    path.closeSubpath()
    return path
}

/// Big Sur template: 824 px body, 185 px corners, centred on 1024, soft shadow.
func macIcon(from art: CGImage) -> CGImage {
    let ctx = bitmap(1024, opaque: false)
    let body = CGRect(x: 100, y: 100, width: 824, height: 824)
    let shape = squircle(body, radius: 185)

    ctx.saveGState()
    ctx.setShadow(offset: CGSize(width: 0, height: -10), blur: 28,
                  color: CGColor(srgbRed: 0, green: 0, blue: 0, alpha: 0.32))
    ctx.addPath(shape)
    ctx.setFillColor(CGColor(srgbRed: 0.07, green: 0.03, blue: 0.2, alpha: 1))
    ctx.fillPath()
    ctx.restoreGState()

    ctx.addPath(shape)
    ctx.clip()
    ctx.draw(art, in: body)
    return ctx.makeImage()!
}

/// Halves repeatedly with high-quality interpolation so small sizes stay crisp.
func downscaled(_ image: CGImage, to size: Int) -> CGImage {
    var current = image
    while current.width > size {
        let next = max(current.width / 2, size)
        let ctx = bitmap(next, opaque: false)
        ctx.draw(current, in: CGRect(x: 0, y: 0, width: next, height: next))
        current = ctx.makeImage()!
    }
    return current
}

// MARK: - Output

func writePNG(_ image: CGImage, _ url: URL) {
    let dest = CGImageDestinationCreateWithURL(url as CFURL, "public.png" as CFString, 1, nil)!
    CGImageDestinationAddImage(dest, image, nil)
    guard CGImageDestinationFinalize(dest) else { fatalError("Could not write \(url.path)") }
}

let outputDir: URL = CommandLine.arguments.count > 1
    ? URL(fileURLWithPath: CommandLine.arguments[1])
    : URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
        .appendingPathComponent("Resources/Assets.xcassets/AppIcon.appiconset")
try FileManager.default.createDirectory(at: outputDir, withIntermediateDirectories: true)

var entries: [[String: Any]] = []

let artwork = renderArtwork(size: 1024, look: .light)
let iosVariants: [(file: String, appearance: String?, image: CGImage)] = [
    ("AppIcon-iOS.png", nil, artwork),
    ("AppIcon-iOS-Dark.png", "dark", renderArtwork(size: 1024, look: .dark)),
    ("AppIcon-iOS-Tinted.png", "tinted", renderArtwork(size: 1024, look: .tinted, grayscale: true)),
]
for variant in iosVariants {
    writePNG(variant.image, outputDir.appendingPathComponent(variant.file))
    var entry: [String: Any] = ["filename": variant.file, "idiom": "universal", "platform": "ios", "size": "1024x1024"]
    if let appearance = variant.appearance {
        entry["appearances"] = [["appearance": "luminosity", "value": appearance]]
    }
    entries.append(entry)
}

let mac = macIcon(from: artwork)
let macSmall = macIcon(from: renderArtwork(size: 1024, look: .small)) // 64 px and below
for points in [16, 32, 128, 256, 512] {
    for scale in [1, 2] {
        let file = "icon_\(points)x\(points)\(scale == 2 ? "@2x" : "").png"
        let pixels = points * scale
        writePNG(downscaled(pixels <= 64 ? macSmall : mac, to: pixels), outputDir.appendingPathComponent(file))
        entries.append(["filename": file, "idiom": "mac", "scale": "\(scale)x", "size": "\(points)x\(points)"])
    }
}

let contents: [String: Any] = ["images": entries, "info": ["author": "xcode", "version": 1]]
let json = try JSONSerialization.data(withJSONObject: contents, options: [.prettyPrinted, .sortedKeys])
try json.write(to: outputDir.appendingPathComponent("Contents.json"))
print("Wrote \(entries.count) icons to \(outputDir.path)")
