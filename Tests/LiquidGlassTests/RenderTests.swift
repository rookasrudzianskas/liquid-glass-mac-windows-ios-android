import LiquidGlass
import SwiftUI
import XCTest

/// End to end: a real `GlassScene` with a `GlassSurface`, rendered by
/// SwiftUI through the Metal layer effect, read back and compared with the
/// CPU model. This checks what the vector tests cannot: that the backdrop
/// copy lines up with the visible backdrop, that colors reach the shader as
/// unpremultiplied sRGB, and that the overlay composites like the spec's
/// `rim + glass(1 − rim.a)` over the backdrop.
@MainActor
final class RenderTests: XCTestCase {
    private let size = CGSize(width: 400, height: 200)
    private let surface = CGSize(width: 200, height: 64)
    /// 8-bit readback of an 8-bit backdrop: about one step in and one out.
    private let tolerance = 3.0 / 255.0

    private func render(material: GlassMaterial, appearance: GlassAppearance) throws -> (pixels: [UInt8], width: Int) {
        let center = CGPoint(x: size.width / 2, y: size.height / 2)
        let backdrop = ShaderLibrary.bundle(.module).lgTestBackdrop(.float2(center))
        let view = GlassScene(material: material, appearance: appearance) {
            GlassContent {
                Rectangle().fill(.white).colorEffect(backdrop)
            }
            GlassSurface(radius: .capsule) {
                Color.clear.frame(width: surface.width, height: surface.height)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .frame(width: size.width, height: size.height)
        .environment(\.displayScale, 1)

        let renderer = ImageRenderer(content: view)
        renderer.scale = 1
        let image = try XCTUnwrap(renderer.cgImage, "ImageRenderer produced no image")
        let width = image.width, height = image.height
        var pixels = [UInt8](repeating: 0, count: width * height * 4)
        let context = try XCTUnwrap(CGContext(
            data: &pixels, width: width, height: height, bitsPerComponent: 8, bytesPerRow: width * 4,
            space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ))
        context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
        return (pixels, width)
    }

    /// The model's color at a pixel center `(px, py)` of the rendered image.
    private func expected(px: Double, py: Double, material: GlassMaterial, dark: Bool) -> SIMD4<Double> {
        let x = px - Double(size.width) / 2, y = py - Double(size.height) / 2
        return GlassOptics.glassPixel(
            x: x, y: y, width: Double(surface.width), height: Double(surface.height), radius: .capsule,
            material: material, dark: dark, refraction: GlassOptics.defaultRefraction, pixel: 1,
            sample: { dx, dy in vectorBackdrop(x + dx, y + dy) }, base: vectorBackdrop(x, y)
        )
    }

    private func check(material: GlassMaterial, appearance: GlassAppearance) throws {
        let (pixels, width) = try render(material: material, appearance: appearance)
        // Pixel centers: inside the face, in the bevel, on the rim and outside.
        let points: [(Int, Int)] = [(200, 100), (130, 100), (285, 100), (200, 72), (200, 128), (110, 100), (60, 30)]
        for (column, row) in points {
            let index = (row * width + column) * 4
            let actual = (0..<4).map { Double(pixels[index + $0]) / 255 }
            let model = expected(px: Double(column) + 0.5, py: Double(row) + 0.5, material: material, dark: appearance == .dark)
            assertClose(actual, model.array, accuracy: tolerance, "\(material) \(appearance) pixel (\(column), \(row))")
        }
    }

    func testClearLight() throws { try check(material: .clear, appearance: .light) }
    func testClearDark() throws { try check(material: .clear, appearance: .dark) }
}
