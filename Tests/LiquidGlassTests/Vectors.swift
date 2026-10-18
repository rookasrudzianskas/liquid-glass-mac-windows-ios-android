import Foundation
import LiquidGlass
import XCTest

/// `spec/vectors.json`, the golden data every platform port must reproduce.
enum Vectors {
    static let url = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .appendingPathComponent("spec/vectors.json")

    static let root: [String: Any] = {
        do {
            let data = try Data(contentsOf: url)
            return try JSONSerialization.jsonObject(with: data) as? [String: Any] ?? [:]
        } catch {
            fatalError("Cannot read \(url.path): \(error)")
        }
    }()

    static func cases(_ key: String) -> [[String: Any]] {
        root[key] as? [[String: Any]] ?? []
    }

    static func object(_ key: String) -> [String: Any] {
        root[key] as? [String: Any] ?? [:]
    }
}

extension Dictionary where Key == String, Value == Any {
    func double(_ key: String) -> Double {
        (self[key] as? NSNumber)?.doubleValue ?? .nan
    }

    func bool(_ key: String) -> Bool {
        (self[key] as? Bool) ?? false
    }

    func doubles(_ key: String) -> [Double] {
        (self[key] as? [NSNumber])?.map(\.doubleValue) ?? []
    }

    func radius(_ key: String = "radius") -> GlassRadius {
        switch self[key] {
        case let name as String where name == "circle": .circle
        case is String: .capsule
        case let number as NSNumber: .points(number.doubleValue)
        default: .points(.nan)
        }
    }

    func material(_ key: String = "material") -> GlassMaterial {
        GlassMaterial(rawValue: self[key] as? String ?? "") ?? .clear
    }

    func rgb(_ key: String) -> SIMD3<Double>? {
        let values = doubles(key)
        return values.count == 3 ? SIMD3(values[0], values[1], values[2]) : nil
    }
}

/// Asserts each component within `accuracy`, naming the failing case.
func assertClose(
    _ actual: [Double], _ expected: [Double], accuracy: Double, _ context: @autoclosure () -> String,
    file: StaticString = #filePath, line: UInt = #line
) {
    XCTAssertEqual(actual.count, expected.count, "component count: \(context())", file: file, line: line)
    for (index, (a, e)) in zip(actual, expected).enumerated() where !(abs(a - e) <= accuracy) {
        XCTFail("component \(index): \(a) vs expected \(e) (±\(accuracy)) — \(context())", file: file, line: line)
        return
    }
}

func assertClose(
    _ actual: Double, _ expected: Double, accuracy: Double, _ context: @autoclosure () -> String,
    file: StaticString = #filePath, line: UInt = #line
) {
    assertClose([actual], [expected], accuracy: accuracy, context(), file: file, line: line)
}

extension SIMD3 where Scalar == Double {
    var array: [Double] { [x, y, z] }
}

extension SIMD4 where Scalar == Double {
    var array: [Double] { [x, y, z, w] }
}

/// The linear test backdrop of the glassPixel vectors, relative to the
/// surface center.
func vectorBackdrop(_ px: Double, _ py: Double) -> SIMD4<Double> {
    SIMD4(min(1, max(0, 0.5 + px / 400)), min(1, max(0, 0.5 + py / 400)), 0.6, 1)
}

/// WCAG 2 contrast ratio of two gamma-encoded sRGB colors, as
/// `spec/generate-vectors.mjs` computes it for the legibility worst cases.
func contrastRatio(_ a: SIMD3<Double>, _ b: SIMD3<Double>) -> Double {
    func luminance(_ c: SIMD3<Double>) -> Double {
        func linear(_ v: Double) -> Double { v <= 0.04045 ? v / 12.92 : pow((v + 0.055) / 1.055, 2.4) }
        return 0.2126 * linear(c.x) + 0.7152 * linear(c.y) + 0.0722 * linear(c.z)
    }
    let x = luminance(a) + 0.05, y = luminance(b) + 0.05
    return max(x, y) / min(x, y)
}

/// `#111113`, the label color on light glass (`spec/OPTICS.md` §8).
let lightGlassInk = SIMD3<Double>(0x11, 0x11, 0x13) / 255

/// The menu label floors of `spec/generate-vectors.mjs` (`legibility.worstCase`):
/// labels over a 6 × 6 × 6 grid of solid backdrops, on plain rows and on
/// highlighted ones.
enum MenuWorstCase {
    /// Every backdrop of the grid, channels in steps of 0.2.
    static let backdrops: [SIMD3<Double>] = {
        let steps = [0, 0.2, 0.4, 0.6, 0.8, 1.0]
        return steps.flatMap { r in steps.flatMap { g in steps.map { b in SIMD3(r, g, b) } } }
    }()

    /// The system colors the tinted floor covers, Apple's light variants then
    /// their dark ones: blue, green, red, orange, yellow, purple, pink,
    /// indigo, teal and mint.
    static let systemTints: [SIMD3<Double>] = [
        [0, 122, 255], [52, 199, 89], [255, 59, 48], [255, 149, 0], [255, 204, 0],
        [175, 82, 222], [255, 45, 85], [88, 86, 214], [90, 200, 250], [0, 199, 190],
        [10, 132, 255], [48, 209, 88], [255, 69, 58], [255, 159, 10], [255, 214, 10],
        [191, 90, 242], [255, 55, 95], [94, 92, 230], [100, 210, 255], [102, 212, 207],
    ].map { SIMD3<Double>($0[0], $0[1], $0[2]) / 255 }

    /// Untinted and tinted menus: Regular and Clear, light and dark.
    static let configurations: [(material: GlassMaterial, dark: Bool)] = [(.regular, false), (.regular, true), (.clear, false), (.clear, true)]

    /// The lower contrast of `label` with the menu glass `glass` and with
    /// the same glass on a highlighted row, the label mixed in at
    /// ``GlassOptics/menuHighlight``.
    static func contrast(glass: SIMD3<Double>, label: SIMD3<Double>) -> Double {
        min(contrastRatio(glass, label), contrastRatio(glass + (label - glass) * GlassOptics.menuHighlight, label))
    }
}
