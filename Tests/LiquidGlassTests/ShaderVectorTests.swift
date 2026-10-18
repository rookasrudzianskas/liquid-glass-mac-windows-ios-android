import LiquidGlass
import Metal
import XCTest

/// The Metal port (`LiquidGlassCore.h`, the functions the glass shader runs)
/// against `spec/vectors.json`, evaluated on the GPU at the spec's GPU
/// tolerance.
final class ShaderVectorTests: XCTestCase {
    private let tolerance = 2e-3
    private var device: MTLDevice!
    private var pipeline: MTLComputePipelineState!
    private var queue: MTLCommandQueue!

    override func setUpWithError() throws {
        guard let device = MTLCreateSystemDefaultDevice() else { throw XCTSkip("No Metal device") }
        let library = try device.makeDefaultLibrary(bundle: Bundle.module)
        guard let function = library.makeFunction(name: "lgTestEvaluate") else { return XCTFail("lgTestEvaluate missing") }
        self.device = device
        pipeline = try device.makeComputePipelineState(function: function)
        queue = device.makeCommandQueue()
    }

    /// Runs one mode of the test kernel over `inputs` (up to 16 floats each).
    private func evaluate(mode: UInt32, _ inputs: [[Float]]) throws -> [SIMD4<Float>] {
        var flat = [Float](repeating: 0, count: inputs.count * 16)
        for (i, input) in inputs.enumerated() {
            precondition(input.count <= 16)
            for (j, value) in input.enumerated() { flat[i * 16 + j] = value }
        }
        var mode = mode
        let input = try XCTUnwrap(device.makeBuffer(bytes: flat, length: flat.count * 4))
        let output = try XCTUnwrap(device.makeBuffer(length: inputs.count * MemoryLayout<SIMD4<Float>>.stride))
        let commands = try XCTUnwrap(queue.makeCommandBuffer())
        let encoder = try XCTUnwrap(commands.makeComputeCommandEncoder())
        encoder.setComputePipelineState(pipeline)
        encoder.setBuffer(input, offset: 0, index: 0)
        encoder.setBuffer(output, offset: 0, index: 1)
        encoder.setBytes(&mode, length: 4, index: 2)
        encoder.dispatchThreads(
            MTLSize(width: inputs.count, height: 1, depth: 1),
            threadsPerThreadgroup: MTLSize(width: min(inputs.count, pipeline.maxTotalThreadsPerThreadgroup), height: 1, depth: 1)
        )
        encoder.endEncoding()
        commands.commit()
        commands.waitUntilCompleted()
        let pointer = output.contents().bindMemory(to: SIMD4<Float>.self, capacity: inputs.count)
        return Array(UnsafeBufferPointer(start: pointer, count: inputs.count))
    }

    private func radius(_ c: [String: Any]) -> Float {
        switch c.radius() {
        case .points(let value): Float(value)
        case .capsule, .circle: -1
        }
    }

    private func flag(_ value: Bool) -> Float { value ? 1 : 0 }

    private func components(_ v: SIMD4<Float>, _ count: Int) -> [Double] {
        (0..<count).map { Double(v[$0]) }
    }

    private func tint(_ c: [String: Any]) -> [Float] {
        if let t = c.rgb("tint") { return [Float(t.x), Float(t.y), Float(t.z), 1] }
        return [0, 0, 0, 0]
    }

    func testShapeField() throws {
        let cases = Vectors.cases("shapeField")
        let results = try evaluate(mode: 0, cases.map { c in
            [Float(c.double("x")), Float(c.double("y")), Float(c.double("width")), Float(c.double("height")), radius(c)]
        })
        for (i, (c, r)) in zip(cases, results).enumerated() {
            assertClose(components(r, 3), [c.double("distance"), c.double("nx"), c.double("ny")], accuracy: tolerance, "GPU shapeField[\(i)] \(c)")
        }
    }

    func testEdgeProfileAndRefraction() throws {
        let edges = Vectors.cases("edgeProfile")
        let edgeResults = try evaluate(mode: 1, edges.map { [Float($0.double("distance")), 0, 0, 0] })
        for (i, (c, r)) in zip(edges, edgeResults).enumerated() {
            assertClose(Double(r.x), c.double("value"), accuracy: tolerance, "GPU edgeProfile[\(i)]")
        }
        let offsets = Vectors.cases("refractionOffset")
        let offsetResults = try evaluate(mode: 1, offsets.map { c in
            [Float(c.double("distance")), Float(c.double("nx")), Float(c.double("ny")), Float(c.double("refraction"))]
        })
        for (i, (c, r)) in zip(offsets, offsetResults).enumerated() {
            assertClose([Double(r.y), Double(r.z)], [c.double("dx"), c.double("dy")], accuracy: tolerance, "GPU refractionOffset[\(i)]")
        }
    }

    func testToneAndMaterialColor() throws {
        let tones = Vectors.cases("tone")
        var inputs: [[Float]] = []
        for c in tones {
            let v = Float(c.double("v"))
            inputs += [[v, v, v, 0, 0, 0], [v, v, v, 0, 1, 0], [v, v, v, 0, 1, 1], [v, v, v, 1, 0, 0], [v, v, v, 1, 1, 0]]
        }
        let results = try evaluate(mode: 2, inputs)
        for (i, c) in tones.enumerated() {
            let expected = [c.double("clearLight"), c.double("clearDark"), c.double("clearDarkNested")]
            assertClose((0..<3).map { Double(results[i * 5 + $0].x) }, expected, accuracy: tolerance, "GPU clear tone[\(i)]")
        }
        let colors = Vectors.cases("materialColor")
        let colorResults = try evaluate(mode: 2, colors.map { c in
            let rgb = c.doubles("color").map(Float.init)
            return rgb + [c.material() == .regular ? 1 : 0, flag(c.bool("dark")), 0]
        })
        for (i, (c, r)) in zip(colors, colorResults).enumerated() {
            assertClose(components(r, 3), c.doubles("value"), accuracy: tolerance, "GPU materialColor[\(i)] \(c)")
        }
    }

    func testTintAndFrost() throws {
        let tints = Vectors.cases("tintColor")
        let tintResults = try evaluate(mode: 3, tints.map { c in
            c.doubles("color").map(Float.init) + c.doubles("tint").map(Float.init) + [c.material() == .regular ? 1 : 0]
        })
        for (i, (c, r)) in zip(tints, tintResults).enumerated() {
            assertClose(components(r, 3), c.doubles("value"), accuracy: tolerance, "GPU tintColor[\(i)] \(c)")
        }
        let frosts = Vectors.cases("frostColor")
        let frostResults = try evaluate(mode: 4, frosts.map { c in
            c.doubles("frost").map(Float.init) + c.doubles("fill").map(Float.init) + [flag(c.bool("dark"))]
        })
        for (i, (c, r)) in zip(frosts, frostResults).enumerated() {
            assertClose(components(r, 3), c.doubles("value"), accuracy: tolerance, "GPU frostColor[\(i)] \(c)")
        }
    }

    /// `lgLegibility`, the menu panels' mix toward the solid backing: the
    /// backing colors (the mix at amount 1), the cases, and the worst-case
    /// label contrast of Regular menu glass over white and over black.
    func testLegibility() throws {
        let legibility = Vectors.object("legibility")
        let backing = legibility["backing"] as? [String: Any] ?? [:]
        let backings = try evaluate(mode: 7, [[0.3, 0.6, 0.9, 0, 1], [0.3, 0.6, 0.9, 1, 1]])
        assertClose(components(backings[0], 3), backing.doubles("light"), accuracy: tolerance, "GPU light backing")
        assertClose(components(backings[1], 3), backing.doubles("dark"), accuracy: tolerance, "GPU dark backing")

        let cases = legibility["cases"] as? [[String: Any]] ?? []
        XCTAssertEqual(cases.count, 6)
        let results = try evaluate(mode: 7, cases.map { c in
            c.doubles("color").map(Float.init) + [flag(c.bool("dark")), Float(c.double("amount"))]
        })
        for (i, (c, r)) in zip(cases, results).enumerated() {
            assertClose(components(r, 3), c.doubles("value"), accuracy: tolerance, "GPU legibility[\(i)] \(c)")
        }

        let amount = Float(legibility.double("menuAmount"))
        let menus = try evaluate(mode: 8, [[1, 1, 1, 1, 1, amount], [0, 0, 0, 1, 0, amount]])
        let worst = legibility["worstCase"] as? [String: Any] ?? [:]
        let darkOverWhite = SIMD3<Double>(Double(menus[0].x), Double(menus[0].y), Double(menus[0].z))
        let lightOverBlack = SIMD3<Double>(Double(menus[1].x), Double(menus[1].y), Double(menus[1].z))
        let white = SIMD3<Double>(1, 1, 1), black = SIMD3<Double>(0, 0, 0)
        assertClose(darkOverWhite.array, GlassOptics.legibilityColor(
            GlassOptics.materialColor(white, material: .regular, dark: true), dark: true, amount: GlassOptics.menuLegibility).array,
            accuracy: tolerance, "GPU dark menu glass over white")
        assertClose(lightOverBlack.array, GlassOptics.legibilityColor(
            GlassOptics.materialColor(black, material: .regular, dark: false), dark: false, amount: GlassOptics.menuLegibility).array,
            accuracy: tolerance, "GPU light menu glass over black")
        // 2e-3 in each channel moves these ratios by at most about 0.05.
        assertClose(contrastRatio(darkOverWhite, white), worst.double("darkOverWhite"), accuracy: 0.06, "GPU white labels on dark menu glass over white")
        assertClose(contrastRatio(lightOverBlack, lightGlassInk), worst.double("lightOverBlack"), accuracy: 0.06, "GPU #111113 labels on light menu glass over black")
        XCTAssertGreaterThanOrEqual(contrastRatio(darkOverWhite, white), 5)
        XCTAssertGreaterThanOrEqual(contrastRatio(lightOverBlack, lightGlassInk), 5)
    }

    func testRimOverlay() throws {
        let cases = Vectors.cases("rimOverlay")
        let results = try evaluate(mode: 5, cases.map { c in
            let pixel = abs(c.double("pixel") - 1.0 / 3.0) < 1e-5 ? 1.0 / 3.0 : c.double("pixel")
            return [Float(c.double("distance")), Float(c.double("nx")), Float(c.double("ny")), flag(c.bool("dark")), Float(pixel)] + tint(c)
        })
        for (i, (c, r)) in zip(cases, results).enumerated() {
            assertClose(components(r, 4), c.doubles("value"), accuracy: tolerance, "GPU rimOverlay[\(i)] \(c)")
        }
    }

    func testGlassPixel() throws {
        let cases = Vectors.object("glassPixel")["cases"] as? [[String: Any]] ?? []
        XCTAssertEqual(cases.count, 63)
        XCTAssertEqual(cases.filter { $0["legibility"] != nil }.count, 6)
        let results = try evaluate(mode: 6, cases.map { c in
            let pixel = abs(c.double("pixel") - 1.0 / 3.0) < 1e-5 ? 1.0 / 3.0 : c.double("pixel")
            return [
                Float(c.double("x")), Float(c.double("y")), Float(c.double("width")), Float(c.double("height")), radius(c),
                c.material() == .regular ? 1 : 0, flag(c.bool("dark")), Float(c.double("refraction")), Float(pixel),
            ] + tint(c) + [c["legibility"] == nil ? 0 : Float(c.double("legibility"))]
        })
        for (i, (c, r)) in zip(cases, results).enumerated() {
            assertClose(components(r, 4), c.doubles("value"), accuracy: tolerance, "GPU glassPixel[\(i)] \(c)")
        }
    }
}
