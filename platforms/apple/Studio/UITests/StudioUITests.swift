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
        XCTAssertTrue(item.waitForExistence(timeout: 3))
        sleep(1)
        snap("menu-open")
        // A tap outside dismisses it.
        app.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.55)).tap()
        sleep(1)
        snap("menu-after-outside-tap")
        XCTAssertTrue(item.waitForNonExistence(timeout: 3))
        // Choosing an item runs it and closes the menu.
        app.buttons["More"].tap()
        XCTAssertTrue(item.waitForExistence(timeout: 3))
        item.tap()
        XCTAssertTrue(app.staticTexts["Refraction"].waitForExistence(timeout: 5))
    }

    func testSwitchToggles() {
        let app = launch("controls")
        let toggle = app.switches["Bluetooth"]
        XCTAssertTrue(toggle.waitForExistence(timeout: 5))
        XCTAssertEqual(toggle.value as? String, "0")
        // Bring the row out from under the tab bar, then tap the switch
        // itself, at the trailing end of its row.
        app.swipeUp(velocity: .slow)
        sleep(1)
        toggle.coordinate(withNormalizedOffset: CGVector(dx: 0.95, dy: 0.5)).tap()
        XCTAssertEqual(toggle.value as? String, "1")
        toggle.coordinate(withNormalizedOffset: CGVector(dx: 0.95, dy: 0.5)).tap()
        XCTAssertEqual(toggle.value as? String, "0")
    }

    func testMenuInScrollViewChangesSelection() {
        let app = launch("controls")
        app.buttons["Sort: Newest"].tap()
        let oldest = app.buttons["Oldest"]
        XCTAssertTrue(oldest.waitForExistence(timeout: 3))
        oldest.tap()
        XCTAssertTrue(app.buttons["Sort: Oldest"].waitForExistence(timeout: 3))
    }

    func testSliderFollowsDrag() {
        let app = launch("controls")
        let slider = app.sliders["Brightness"]
        XCTAssertTrue(slider.waitForExistence(timeout: 5))
        let before = slider.value as? String
        let start = slider.coordinate(withNormalizedOffset: CGVector(dx: 0.6, dy: 0.5))
        start.press(forDuration: 0.3, thenDragTo: slider.coordinate(withNormalizedOffset: CGVector(dx: 0.1, dy: 0.5)))
        XCTAssertNotEqual(slider.value as? String, before)
    }

    func testStoriesScroll() {
        let app = launch("blur")
        let title = app.staticTexts["Stories"]
        XCTAssertTrue(title.waitForExistence(timeout: 5))
        let top = title.frame.minY
        app.swipeUp(velocity: .slow)
        sleep(2)
        snap("stories-scrolled")
        XCTAssertLessThan(title.frame.minY, top - 100)
        // The toolbar scrolls back to the top.
        app.buttons["Scroll to top"].tap()
        sleep(2)
        XCTAssertEqual(title.frame.minY, top, accuracy: 2)
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
