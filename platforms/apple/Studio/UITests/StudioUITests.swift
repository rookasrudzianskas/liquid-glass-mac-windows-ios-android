import XCTest

/// Drives Liquid Glass Studio like a person would: navigation, menus,
/// switches, sliders, presses and scrolling. Set `LG_SHOTS` (for example
/// `TEST_RUNNER_LG_SHOTS=/tmp/shots xcodebuild test …`) to also save a
/// screenshot after each step.
final class StudioUITests: XCTestCase {
    override func setUp() {
        continueAfterFailure = false
    }

    private func launch(_ page: String, appearance: String = "dark") -> XCUIApplication {
        let app = XCUIApplication()
        app.launchArguments = ["-page", page, "-appearance", appearance]
        app.launch()
        return app
    }

    /// Waits until `condition` holds, polling the UI for up to `timeout` seconds.
    private func waitUntil(timeout: TimeInterval = 8, _ condition: () -> Bool) -> Bool {
        let deadline = Date().addingTimeInterval(timeout)
        while Date() < deadline {
            if condition() { return true }
            RunLoop.current.run(until: Date().addingTimeInterval(0.2))
        }
        return condition()
    }

    /// Lets animations run for `seconds` without asserting anything.
    private func settle(_ seconds: TimeInterval = 1) {
        RunLoop.current.run(until: Date().addingTimeInterval(seconds))
    }

    private func snap(_ name: String) {
        guard let directory = ProcessInfo.processInfo.environment["LG_SHOTS"] else { return }
        let url = URL(fileURLWithPath: directory).appendingPathComponent("ui-\(name).png")
        try? FileManager.default.createDirectory(atPath: directory, withIntermediateDirectories: true)
        try? XCUIScreen.main.screenshot().pngRepresentation.write(to: url)
    }

    func testTabBarSwitchesPages() {
        let app = launch("hero")
        XCTAssertTrue(app.staticTexts["Liquid Glass"].waitForExistence(timeout: 5))
        app.buttons["Controls"].firstMatch.tap()
        XCTAssertTrue(app.staticTexts["Controls"].waitForExistence(timeout: 5))
        snap("tab-controls")
        app.buttons["Lab"].firstMatch.tap()
        XCTAssertTrue(app.staticTexts["Refraction"].waitForExistence(timeout: 5))
        app.buttons["Blur"].firstMatch.tap()
        XCTAssertTrue(app.staticTexts["Stories"].waitForExistence(timeout: 5))
        app.buttons["Hero"].firstMatch.tap()
        XCTAssertTrue(app.staticTexts["Liquid Glass"].waitForExistence(timeout: 5))
    }

    func testMenuOpensChoosesAndDismisses() {
        let app = launch("hero")
        app.buttons["More"].tap()
        let item = app.buttons["Materials lab"]
        XCTAssertTrue(item.waitForExistence(timeout: 5))
        // Let the panel finish growing out of its trigger.
        settle(1)
        snap("menu-open")
        // A tap outside dismisses it.
        app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.55)).tap()
        XCTAssertTrue(item.waitForNonExistence(timeout: 5))
        // Choosing an item runs it and closes the menu.
        app.buttons["More"].tap()
        XCTAssertTrue(item.waitForExistence(timeout: 5))
        settle(1)
        item.tap()
        XCTAssertTrue(app.staticTexts["Refraction"].waitForExistence(timeout: 5))
    }

    func testSwitchToggles() {
        let app = launch("controls")
        let toggle = app.switches["Bluetooth"]
        XCTAssertTrue(toggle.waitForExistence(timeout: 5))
        XCTAssertEqual(toggle.value as? String, "0")
        // Bring the row well clear of the floating tab bar, then tap the
        // switch itself, at the trailing end of its row.
        let clear = app.frame.height * 0.6
        for _ in 0..<6 where toggle.frame.maxY > clear {
            app.swipeUp(velocity: .slow)
            settle(2)
        }
        XCTAssertLessThan(toggle.frame.maxY, clear)
        toggle.coordinate(withNormalizedOffset: CGVector(dx: 0.95, dy: 0.5)).tap()
        XCTAssertTrue(waitUntil { toggle.value as? String == "1" }, "switch did not turn on")
        toggle.coordinate(withNormalizedOffset: CGVector(dx: 0.95, dy: 0.5)).tap()
        XCTAssertTrue(waitUntil { toggle.value as? String == "0" }, "switch did not turn off")
    }

    func testMenuInScrollViewChangesSelection() {
        let app = launch("controls")
        let sort = app.buttons["Sort: Newest"]
        XCTAssertTrue(sort.waitForExistence(timeout: 5))
        sort.tap()
        let oldest = app.buttons["Oldest"]
        XCTAssertTrue(oldest.waitForExistence(timeout: 5))
        settle(1)
        oldest.tap()
        XCTAssertTrue(app.buttons["Sort: Oldest"].waitForExistence(timeout: 5))
    }

    func testSliderFollowsDrag() {
        let app = launch("controls")
        let slider = app.sliders["Brightness"]
        XCTAssertTrue(slider.waitForExistence(timeout: 5))
        let before = slider.value as? String
        let start = slider.coordinate(withNormalizedOffset: CGVector(dx: 0.6, dy: 0.5))
        start.press(forDuration: 0.3, thenDragTo: slider.coordinate(withNormalizedOffset: CGVector(dx: 0.1, dy: 0.5)))
        XCTAssertTrue(waitUntil { slider.value as? String != before }, "slider value did not change")
    }

    func testStoriesScroll() {
        let app = launch("blur")
        let title = app.staticTexts["Stories"]
        XCTAssertTrue(title.waitForExistence(timeout: 5))
        let top = title.frame.minY
        app.swipeUp(velocity: .slow)
        XCTAssertTrue(waitUntil { title.frame.minY < top - 100 }, "stories did not scroll")
        settle(2)
        snap("stories-scrolled")
        // The toolbar scrolls back to the top.
        app.buttons["Scroll to top"].tap()
        XCTAssertTrue(waitUntil { abs(title.frame.minY - top) < 2 }, "did not return to the top")
    }

    /// Holds a press on the hero card and drags it, long enough to capture
    /// the grown, stretched and lit glass from outside the test.
    func testCardPressAndDrag() {
        let app = launch("hero")
        let card = app.staticTexts["Liquid Glass"]
        XCTAssertTrue(card.waitForExistence(timeout: 5))
        let start = card.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.5))
        start.press(
            forDuration: Double(ProcessInfo.processInfo.environment["LG_HOLD"] ?? "0.5") ?? 0.5,
            thenDragTo: start.withOffset(CGVector(dx: 60, dy: 120)),
            withVelocity: .slow,
            thenHoldForDuration: Double(ProcessInfo.processInfo.environment["LG_HOLD"] ?? "0.5") ?? 0.5
        )
        XCTAssertTrue(card.exists)
    }
}
