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
