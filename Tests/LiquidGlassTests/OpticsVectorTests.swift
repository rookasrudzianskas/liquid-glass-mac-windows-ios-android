import LiquidGlass
import XCTest

/// The Swift CPU port against every category of `spec/vectors.json`, at the
/// spec's CPU tolerance.
final class OpticsVectorTests: XCTestCase {
    private let tolerance = 1e-4

    /// JSON stores 1/3 rounded to six digits; the generator used the exact value.
    private func pixel(_ value: Double) -> Double {
        abs(value - 1.0 / 3.0) < 1e-5 ? 1.0 / 3.0 : value
    }

    func testVectorsLoad() {
        XCTAssertFalse(Vectors.cases("shapeField").isEmpty, "vectors.json not found at \(Vectors.url.path)")
    }

    func testConstants() {
        let constants = Vectors.object("constants")
        XCTAssertEqual(GlassOptics.bevel, constants.double("bevel"))
        XCTAssertEqual(GlassOptics.defaultRefraction, constants.double("defaultRefraction"))
        XCTAssertEqual(GlassOptics.continuousExtent, constants.double("continuousExtent"))
        XCTAssertEqual(GlassOptics.continuousExponent, constants.double("continuousExponent"))
        assertClose(GlassOptics.luma.array, constants.doubles("luma"), accuracy: 0, "luma")

        let materials = Vectors.object("materials")
        let clear = materials["clear"] as? [String: Any] ?? [:]
        let regular = materials["regular"] as? [String: Any] ?? [:]
        XCTAssertEqual(GlassOptics.tintBody(.clear), clear.double("tintBody"))
        XCTAssertEqual(GlassOptics.tintBody(.regular), regular.double("tintBody"))
        XCTAssertEqual(GlassOptics.frostSigma, regular.double("blur"))
        for (dark, key) in [(false, "light"), (true, "dark")] {
            let spec = regular[key] as? [String: Any] ?? [:]
            XCTAssertEqual(GlassOptics.fillSigma(dark: dark), spec.double("fillSigma"))
        }
    }

    func testShapeParameters() {
        for (i, c) in Vectors.cases("shapeParams").enumerated() {
            let p = GlassOptics.shapeParameters(width: c.double("width"), height: c.double("height"), radius: c.radius())
            assertClose([p.extent, p.exponent], [c.double("extent"), c.double("exponent")], accuracy: tolerance, "shapeParams[\(i)] \(c)")
        }
    }

    func testShapeField() {
        let cases = Vectors.cases("shapeField")
        XCTAssertEqual(cases.count, 640)
        for (i, c) in cases.enumerated() {
            let w = c.double("width"), h = c.double("height")
            let p = GlassOptics.shapeParameters(width: w, height: h, radius: c.radius())
            let f = GlassOptics.shapeField(x: c.double("x"), y: c.double("y"), width: w, height: h, extent: p.extent, exponent: p.exponent)
            assertClose([f.distance, f.nx, f.ny], [c.double("distance"), c.double("nx"), c.double("ny")], accuracy: tolerance, "shapeField[\(i)] \(c)")
        }
    }

    func testOutline() {
        let o = Vectors.object("outline")
        let points = GlassOptics.shapeOutline(
            width: o.double("width"), height: o.double("height"), radius: o.radius(), segments: Int(o.double("segments"))
        )
        let expected = o["points"] as? [[NSNumber]] ?? []
        XCTAssertEqual(points.count, expected.count)
        for (i, (point, e)) in zip(points, expected).enumerated() {
            assertClose([Double(point.x), Double(point.y)], e.map(\.doubleValue), accuracy: tolerance, "outline[\(i)]")
        }
    }

    func testEdgeProfileAndRefraction() {
        for (i, c) in Vectors.cases("edgeProfile").enumerated() {
            assertClose(GlassOptics.edgeProfile(distance: c.double("distance")), c.double("value"), accuracy: tolerance, "edgeProfile[\(i)]")
        }
        for (i, c) in Vectors.cases("refractionOffset").enumerated() {
            let o = GlassOptics.refractionOffset(distance: c.double("distance"), nx: c.double("nx"), ny: c.double("ny"), refraction: c.double("refraction"))
            assertClose([Double(o.dx), Double(o.dy)], [c.double("dx"), c.double("dy")], accuracy: tolerance, "refractionOffset[\(i)]")
        }
    }

    func testDefaultRefraction() {
        let cases = Vectors.cases("defaultRefraction")
        XCTAssertEqual(cases.count, 9)
        for (i, c) in cases.enumerated() {
            let value = GlassOptics.defaultRefraction(width: c.double("width"), height: c.double("height"))
            assertClose(value, c.double("value"), accuracy: tolerance, "defaultRefraction[\(i)] \(c)")
        }
    }

    func testCoverage() {
        XCTAssertEqual(GlassOptics.coverage(distance: 0, pixel: 1.0 / 3.0), 0.5)
        XCTAssertEqual(GlassOptics.coverage(distance: -1, pixel: 1.0 / 3.0), 1)
        XCTAssertEqual(GlassOptics.coverage(distance: 1, pixel: 1.0 / 3.0), 0)
    }

    func testTone() {
        for (i, c) in Vectors.cases("tone").enumerated() {
            let v = c.double("v")
            assertClose(
                [GlassOptics.clearTone(v, dark: false), GlassOptics.clearTone(v, dark: true), GlassOptics.clearTone(v, dark: true, nested: true),
                 GlassOptics.regularTone(v, dark: false), GlassOptics.regularTone(v, dark: true)],
                [c.double("clearLight"), c.double("clearDark"), c.double("clearDarkNested"), c.double("regularLight"), c.double("regularDark")],
                accuracy: tolerance, "tone[\(i)] v=\(v)"
            )
        }
    }

    func testMaterialColor() {
        for (i, c) in Vectors.cases("materialColor").enumerated() {
            guard let color = c.rgb("color") else { return XCTFail("materialColor[\(i)] color") }
            let value = GlassOptics.materialColor(color, material: c.material(), dark: c.bool("dark"))
            assertClose(value.array, c.doubles("value"), accuracy: tolerance, "materialColor[\(i)] \(c)")
        }
    }

    func testTintColor() {
        for (i, c) in Vectors.cases("tintColor").enumerated() {
            guard let color = c.rgb("color"), let tint = c.rgb("tint") else { return XCTFail("tintColor[\(i)] inputs") }
            let value = GlassOptics.tintColor(color, tint: tint, material: c.material())
            assertClose(value.array, c.doubles("value"), accuracy: tolerance, "tintColor[\(i)] \(c)")
        }
    }

    func testFrostColor() {
        for (i, c) in Vectors.cases("frostColor").enumerated() {
            guard let frost = c.rgb("frost"), let fill = c.rgb("fill") else { return XCTFail("frostColor[\(i)] inputs") }
            let value = GlassOptics.frostColor(frost: frost, fill: fill, dark: c.bool("dark"))
            assertClose(value.array, c.doubles("value"), accuracy: tolerance, "frostColor[\(i)] \(c)")
        }
    }

    /// The legibility backing of menu panels (`spec/OPTICS.md` §3): the
    /// menu amount and highlight, the backing colors, the mix and the
    /// label contrast it gives over a white or a black backdrop.
    func testLegibility() {
        let legibility = Vectors.object("legibility")
        XCTAssertFalse(legibility.isEmpty, "legibility section missing")
        XCTAssertEqual(GlassOptics.menuLegibility, legibility.double("menuAmount"))
        XCTAssertEqual(GlassOptics.menuHighlight, legibility.double("highlight"))
        let backing = legibility["backing"] as? [String: Any] ?? [:]
        assertClose(GlassOptics.legibilityBacking(dark: false).array, backing.doubles("light"), accuracy: tolerance, "light backing")
        assertClose(GlassOptics.legibilityBacking(dark: true).array, backing.doubles("dark"), accuracy: tolerance, "dark backing")
        let cases = legibility["cases"] as? [[String: Any]] ?? []
        XCTAssertEqual(cases.count, 6)
        for (i, c) in cases.enumerated() {
            guard let color = c.rgb("color") else { return XCTFail("legibility[\(i)] color") }
            let value = GlassOptics.legibilityColor(color, dark: c.bool("dark"), amount: c.double("amount"))
            assertClose(value.array, c.doubles("value"), accuracy: tolerance, "legibility[\(i)] \(c)")
        }
        let worst = legibility["worstCase"] as? [String: Any] ?? [:]
        let white = SIMD3<Double>(1, 1, 1), black = SIMD3<Double>(0, 0, 0)
        let darkOverWhite = GlassOptics.legibilityColor(
            GlassOptics.materialColor(white, material: .regular, dark: true), dark: true, amount: GlassOptics.menuLegibility)
        let lightOverBlack = GlassOptics.legibilityColor(
            GlassOptics.materialColor(black, material: .regular, dark: false), dark: false, amount: GlassOptics.menuLegibility)
        assertClose(contrastRatio(darkOverWhite, white), worst.double("darkOverWhite"), accuracy: tolerance, "white labels on dark menu glass over white")
        assertClose(contrastRatio(lightOverBlack, lightGlassInk), worst.double("lightOverBlack"), accuracy: tolerance, "#111113 labels on light menu glass over black")
        XCTAssertGreaterThanOrEqual(contrastRatio(darkOverWhite, white), 5.2)
        XCTAssertGreaterThanOrEqual(contrastRatio(lightOverBlack, lightGlassInk), 5.2)
    }

    /// The label color of menus: the appearance foreground untinted, white
    /// or `#111113` by contrast with the tint mixed toward the backing.
    func testMenuForeground() {
        let cases = Vectors.object("legibility")["menuForeground"] as? [[String: Any]] ?? []
        XCTAssertEqual(cases.count, 8)
        XCTAssertEqual(cases.filter { $0["tint"] is NSNull }.count, 2, "untinted cases")
        for (i, c) in cases.enumerated() {
            let value = GlassOptics.menuForeground(tint: c.rgb("tint"), material: c.material(), dark: c.bool("dark"))
            assertClose(value.array, c.doubles("value"), accuracy: tolerance, "menuForeground[\(i)] \(c)")
        }
        // Without the backing, the tinted rule is the one of tinted glass
        // (OPTICS §8): a light blue Regular menu would take white labels.
        let blue = SIMD3<Double>(0, 0.478, 1)
        assertClose(GlassOptics.menuForeground(tint: blue, material: .regular, dark: false, legibility: 0).array, [1, 1, 1], accuracy: 0, "blue without the backing")
        assertClose(GlassOptics.menuForeground(tint: blue, material: .regular, dark: false).array, lightGlassInk.array, accuracy: tolerance, "blue with the backing")
    }

    /// The label floors (`legibility.worstCase`): over a 6 × 6 × 6 grid of
    /// solid backdrops, plain and highlighted rows of untinted menus keep at
    /// least 5.2:1, and of menus tinted with the system colors of either
    /// appearance at least 5.0:1 (5.07:1, the dark variant of yellow), Regular
    /// and Clear, light and dark.
    func testMenuWorstCase() {
        let worst = Vectors.object("legibility")["worstCase"] as? [String: Any] ?? [:]
        func floor(_ material: GlassMaterial, dark: Bool, tint: SIMD3<Double>?) -> Double {
            let label = GlassOptics.menuForeground(tint: tint, material: material, dark: dark)
            return MenuWorstCase.backdrops.map { backdrop in
                var rgb = GlassOptics.materialColor(backdrop, material: material, dark: dark)
                if let tint { rgb = GlassOptics.tintColor(rgb, tint: tint, material: material) }
                let glass = GlassOptics.legibilityColor(rgb, dark: dark, amount: GlassOptics.menuLegibility)
                return MenuWorstCase.contrast(glass: glass, label: label)
            }.min() ?? 0
        }
        let untinted = MenuWorstCase.configurations.map { floor($0.material, dark: $0.dark, tint: nil) }.min() ?? 0
        let tinted = MenuWorstCase.configurations.flatMap { c in
            MenuWorstCase.systemTints.map { floor(c.material, dark: c.dark, tint: $0) }
        }.min() ?? 0
        assertClose(untinted, worst.double("untintedWithHighlight"), accuracy: tolerance, "untinted menus with the highlight")
        assertClose(tinted, worst.double("systemTintsWithHighlight"), accuracy: tolerance, "system tints with the highlight")
        XCTAssertGreaterThanOrEqual(untinted, 5.2)
        XCTAssertGreaterThanOrEqual(tinted, 5.0)
    }

    func testRimOverlay() {
        let cases = Vectors.cases("rimOverlay")
        XCTAssertEqual(cases.count, 328)
        for (i, c) in cases.enumerated() {
            let value = GlassOptics.rimOverlay(
                distance: c.double("distance"), nx: c.double("nx"), ny: c.double("ny"), dark: c.bool("dark"),
                pixel: pixel(c.double("pixel")), tint: c.rgb("tint")
            )
            assertClose(value.array, c.doubles("value"), accuracy: tolerance, "rimOverlay[\(i)] \(c)")
        }
    }

    func testGlassPixel() {
        let cases = Vectors.object("glassPixel")["cases"] as? [[String: Any]] ?? []
        XCTAssertEqual(cases.count, 63)
        // Six menu panel pixels, light and dark, carry the open menu's
        // legibility backing.
        XCTAssertEqual(cases.filter { $0["legibility"] != nil }.count, 6)
        XCTAssertEqual(cases.compactMap { $0["legibility"] as? Double }, Array(repeating: GlassOptics.menuLegibility, count: 6))
        for (i, c) in cases.enumerated() {
            let x = c.double("x"), y = c.double("y")
            let value = GlassOptics.glassPixel(
                x: x, y: y, width: c.double("width"), height: c.double("height"), radius: c.radius(),
                material: c.material(), dark: c.bool("dark"), refraction: c.double("refraction"),
                tint: c.rgb("tint"), pixel: pixel(c.double("pixel")), legibility: c["legibility"] == nil ? 0 : c.double("legibility"),
                sample: { dx, dy in vectorBackdrop(x + dx, y + dy) }, base: vectorBackdrop(x, y)
            )
            assertClose(value.array, c.doubles("value"), accuracy: tolerance, "glassPixel[\(i)] \(c)")
        }
    }

    func testSprings() {
        let springs = Vectors.object("springs")
        let named: [String: GlassSpring] = [
            "press": .press, "release": .release, "glow": .glow, "morph": .morph, "layout": .layout, "track": .track,
        ]
        XCTAssertEqual(Set(springs.keys), Set(named.keys))
        for (name, spring) in named {
            let spec = springs[name] as? [String: Any] ?? [:]
            XCTAssertEqual(spring.duration, spec.double("duration"), name)
            XCTAssertEqual(spring.bounce, spec.double("bounce"), name)
            var state = GlassSpring.State(value: spec.double("from"), target: spec.double("to"))
            let dt = 1.0 / 60.0, every = Int(spec.double("sampleEvery"))
            var values: [Double] = []
            for frame in 1...90 {
                state = spring.step(state, dt: dt)
                if frame % every == 0 { values.append(state.value) }
            }
            assertClose(values, spec.doubles("values"), accuracy: tolerance, "spring \(name)")
        }
    }

    func testSpringCoefficients() {
        let spring = GlassSpring(duration: 0.5, bounce: 0.42)
        XCTAssertEqual(spring.stiffness, pow(2 * .pi / 0.5, 2), accuracy: 1e-9)
        XCTAssertEqual(spring.damping, 4 * .pi * (1 - 0.42) / 0.5, accuracy: 1e-9)
        XCTAssertEqual(GlassSpring(duration: 0.5, bounce: -0.5).damping, 4 * .pi / (0.5 * 0.5), accuracy: 1e-9)
        XCTAssertEqual(GlassSpring.release.critical.bounce, 0)
    }

    func testRubberBandAndStretchLimit() {
        for (i, c) in Vectors.cases("rubberBand").enumerated() {
            assertClose(GlassOptics.rubberBand(c.double("offset"), limit: c.double("limit")), c.double("value"), accuracy: tolerance, "rubberBand[\(i)]")
        }
        for (i, c) in Vectors.cases("stretchLimit").enumerated() {
            assertClose(GlassOptics.stretchLimit(width: c.double("width"), height: c.double("height")), c.double("value"), accuracy: tolerance, "stretchLimit[\(i)]")
        }
    }

    func testPressScale() {
        for (i, c) in Vectors.cases("pressScale").enumerated() {
            let s = GlassOptics.pressScale(
                width: c.double("width"), height: c.double("height"), press: c.double("press"),
                tx: c.double("tx"), ty: c.double("ty"), vx: c.double("vx"), vy: c.double("vy"), reduced: c.bool("reduced")
            )
            assertClose([Double(s.width), Double(s.height)], [c.double("sx"), c.double("sy")], accuracy: tolerance, "pressScale[\(i)] \(c)")
        }
    }

    func testGlow() {
        for (i, c) in Vectors.cases("glow").enumerated() {
            let g = GlassOptics.glow(width: c.double("width"), height: c.double("height"), dark: c.bool("dark"))
            assertClose([g.radius, g.alpha, g.hover], [c.double("radius"), c.double("alpha"), c.double("hover")], accuracy: tolerance, "glow[\(i)]")
        }
    }

    func testProgressive() {
        let progressive = Vectors.object("progressive")
        XCTAssertEqual(Double(GlassOptics.progressiveLevels), progressive.double("levels"))
        let blurs = (0...GlassOptics.progressiveLevels).map { GlassOptics.progressiveLevelBlur(level: $0, blur: 20) }
        assertClose(blurs, progressive.doubles("blurForLevelsAt20"), accuracy: tolerance, "progressive blur levels")
        for (i, c) in (progressive["weights"] as? [[String: Any]] ?? []).enumerated() {
            let u = c.double("u")
            let weights = (0...GlassOptics.progressiveLevels).map { GlassOptics.progressiveWeight(level: $0, u: u) }
            assertClose(weights, c.doubles("weights"), accuracy: tolerance, "progressive[\(i)] u=\(u)")
            XCTAssertEqual(weights.reduce(0, +), 1, accuracy: 1e-9)
        }
    }
}
