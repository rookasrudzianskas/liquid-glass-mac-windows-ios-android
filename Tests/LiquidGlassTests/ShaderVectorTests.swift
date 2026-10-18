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

    /// Runs one mode of the test kernel over `inputs` (up to 16 floats each),
    /// one threadgroup at a time.
    private func evaluate(mode: UInt32, _ inputs: [[Float]]) throws -> [SIMD4<Float>] {
        let group = pipeline.maxTotalThreadsPerThreadgroup
        if inputs.count > group {
            return try stride(from: 0, to: inputs.count, by: group).flatMap { start in
                try evaluate(mode: mode, Array(inputs[start..<min(start + group, inputs.count)]))
            }
        }
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
    /// backing colors (the mix at amount 1), the cases, and the label
    /// contrast of Regular menu glass over white and over black.
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
        let darkOverWhite = rgb(menus[0]), lightOverBlack = rgb(menus[1])
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
        XCTAssertGreaterThanOrEqual(contrastRatio(darkOverWhite, white), 5.2)
        XCTAssertGreaterThanOrEqual(contrastRatio(lightOverBlack, lightGlassInk), 5.2)
    }

    /// The input of the test kernel's mode 9, the menu glass in the
    /// shader's order: the material color of `rgb` (when `materialColor` is
    /// set), the tint, then the backing mix.
    private func menuGlass(
        _ rgb: SIMD3<Double>, material: GlassMaterial, dark: Bool, tint: SIMD3<Double>?, legibility: Double, materialColor: Bool
    ) -> [Float] {
        let t = tint ?? .zero
        return [
            Float(rgb.x), Float(rgb.y), Float(rgb.z), material == .regular ? 1 : 0, flag(dark),
            Float(t.x), Float(t.y), Float(t.z), tint == nil ? 0 : 1, Float(legibility), materialColor ? 1 : 0,
        ]
    }

    private func rgb(_ v: SIMD4<Float>) -> SIMD3<Double> { SIMD3(Double(v.x), Double(v.y), Double(v.z)) }

    /// `menuForeground` on the shader's glass: the tint as the shader draws
    /// it over a mid-gray backdrop (whose luma is 0.5) and mixed toward the
    /// backing, which picks the same label as the vectors.
    func testMenuForeground() throws {
        let legibility = Vectors.object("legibility")
        let cases = (legibility["menuForeground"] as? [[String: Any]] ?? []).filter { $0.rgb("tint") != nil }
        XCTAssertEqual(cases.count, 6)
        let amount = legibility.double("menuAmount")
        let gray = SIMD3<Double>(repeating: 0.5)
        let results = try evaluate(mode: 9, cases.map { c in
            menuGlass(gray, material: c.material(), dark: c.bool("dark"), tint: c.rgb("tint"), legibility: amount, materialColor: false)
        })
        for (i, (c, r)) in zip(cases, results).enumerated() {
            guard let tint = c.rgb("tint") else { continue }
            let body = GlassOptics.tintBody(c.material())
            let expected = GlassOptics.legibilityColor(tint * (body + (1 - body) * 0.5), dark: c.bool("dark"), amount: amount)
            let glass = rgb(r)
            assertClose(glass.array, expected.array, accuracy: tolerance, "GPU menu glass of menuForeground[\(i)] \(c)")
            let label = contrastRatio(glass, lightGlassInk) > contrastRatio(glass, SIMD3(1, 1, 1)) ? lightGlassInk : SIMD3(1, 1, 1)
            assertClose(label.array, c.doubles("value"), accuracy: 1e-4, "GPU label of menuForeground[\(i)] \(c)")
        }
    }

    /// The label floors (`legibility.worstCase`) on the shader's menu glass:
    /// over a 6 × 6 × 6 grid of solid backdrops, plain and highlighted rows
    /// of untinted menus and of menus tinted with the system colors, Regular
    /// and Clear, light and dark, keep at least 5.2:1.
    func testMenuWorstCase() throws {
        let legibility = Vectors.object("legibility")
        let worst = legibility["worstCase"] as? [String: Any] ?? [:]
        let amount = legibility.double("menuAmount")
        var inputs: [[Float]] = [], labels: [SIMD3<Double>] = [], tinted: [Bool] = []
        for c in MenuWorstCase.configurations {
            for tint in [nil] + MenuWorstCase.systemTints.map(Optional.some) {
                let label = GlassOptics.menuForeground(tint: tint, material: c.material, dark: c.dark)
                for backdrop in MenuWorstCase.backdrops {
                    inputs.append(menuGlass(backdrop, material: c.material, dark: c.dark, tint: tint, legibility: amount, materialColor: true))
                    labels.append(label)
                    tinted.append(tint != nil)
                }
            }
        }
        XCTAssertEqual(inputs.count, 4 * 11 * 216)
        let results = try evaluate(mode: 9, inputs)
        var untintedFloor = Double.infinity, tintedFloor = Double.infinity
        for (i, r) in results.enumerated() {
            let ratio = MenuWorstCase.contrast(glass: rgb(r), label: labels[i])
            if tinted[i] { tintedFloor = min(tintedFloor, ratio) } else { untintedFloor = min(untintedFloor, ratio) }
        }
        // 2e-3 in each channel moves these ratios by at most about 0.05.
        assertClose(untintedFloor, worst.double("untintedWithHighlight"), accuracy: 0.06, "GPU untinted menus with the highlight")
        assertClose(tintedFloor, worst.double("systemTintsWithHighlight"), accuracy: 0.06, "GPU system tints with the highlight")
        XCTAssertGreaterThanOrEqual(untintedFloor, 5.2)
        XCTAssertGreaterThanOrEqual(tintedFloor, 5.2)
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
