import XCTest

/// Drives Liquid Glass Studio like a person would: navigation, menus,
/// switches, sliders, presses and scrolling. UI automation also lists the
/// copies of the backdrop inside every glass surface (hidden from
/// VoiceOver), so queries for backdrop text take the first match, the
/// visible backdrop. Set `LG_SHOTS` (for example
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

    /// Drags content upward by a finger-like path from 75% to 30% of the
    /// screen height, starting away from any control.
    private func dragUp(_ app: XCUIApplication, x: CGFloat = 0.5) {
        let start = app.coordinate(withNormalizedOffset: CGVector(dx: x, dy: 0.75))
        let end = app.coordinate(withNormalizedOffset: CGVector(dx: x, dy: 0.3))
        start.press(forDuration: 0.05, thenDragTo: end, withVelocity: .default, thenHoldForDuration: 0.05)
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
            dragUp(app, x: 0.03)
            settle(1)
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

    /// Glass controls track presses without stealing the scroll view's pan.
    func testScrollStartingOnGlassButtonScrolls() {
        let app = launch("controls")
        let button = app.buttons["Large"].firstMatch
        XCTAssertTrue(button.waitForExistence(timeout: 5))
        let top = button.frame.minY
        let start = button.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.5))
        start.press(forDuration: 0.05, thenDragTo: start.withOffset(CGVector(dx: 0, dy: -300)), withVelocity: .default, thenHoldForDuration: 0.05)
        XCTAssertTrue(waitUntil { button.frame.minY < top - 100 }, "the scroll view did not scroll from a glass button")
    }

    /// A vertical swipe that starts on a slider scrolls the page and leaves
    /// the value alone, like the system's sliders.
    func testVerticalSwipeOnSliderScrolls() {
        let app = launch("controls")
        let slider = app.sliders["Brightness"]
        XCTAssertTrue(slider.waitForExistence(timeout: 5))
        let before = slider.value as? String
        let top = slider.frame.minY
        let start = slider.coordinate(withNormalizedOffset: CGVector(dx: 0.3, dy: 0.5))
        start.press(forDuration: 0.05, thenDragTo: start.withOffset(CGVector(dx: 0, dy: -250)), withVelocity: .default, thenHoldForDuration: 0.05)
        XCTAssertTrue(waitUntil { slider.frame.minY < top - 100 }, "the page did not scroll from a slider")
        settle(1)
        XCTAssertEqual(slider.value as? String, before, "a vertical swipe changed the slider")
    }

    /// A vertical swipe that starts on a tab bar scrolls the page and keeps
    /// the selection.
    func testVerticalSwipeOnTabBarScrolls() {
        let app = launch("controls")
        let week = app.buttons["Week"].firstMatch
        XCTAssertTrue(week.waitForExistence(timeout: 5))
        // Above the floating tab bar (the section is near the page's end).
        let clear = app.frame.height * 0.8
        for _ in 0..<6 where week.frame.maxY > clear {
            dragUp(app, x: 0.03)
            settle(1)
        }
        XCTAssertLessThan(week.frame.maxY, clear)
        XCTAssertTrue(week.isSelected)
        let top = week.frame.minY
        let start = week.coordinate(withNormalizedOffset: CGVector(dx: 0.5, dy: 0.5))
        start.press(forDuration: 0.05, thenDragTo: start.withOffset(CGVector(dx: 0, dy: 250)), withVelocity: .default, thenHoldForDuration: 0.05)
        XCTAssertTrue(waitUntil { week.frame.minY > top + 100 }, "the page did not scroll from a tab bar")
        settle(1)
        XCTAssertTrue(week.isSelected, "a vertical swipe changed the tab")
    }

    /// The stories' Filter menu shows one category, then all of them again.
    func testStoriesFilter() {
        let app = launch("blur")
        XCTAssertTrue(app.staticTexts["Stories"].firstMatch.waitForExistence(timeout: 5))
        app.buttons["Filter"].tap()
        let optics = app.buttons["Optics"]
        XCTAssertTrue(optics.waitForExistence(timeout: 5))
        settle(1)
        optics.tap()
        XCTAssertTrue(app.staticTexts["Optics"].firstMatch.waitForExistence(timeout: 5), "the filtered heading")
        app.buttons["Filter"].tap()
        let all = app.buttons["All stories"]
        XCTAssertTrue(all.waitForExistence(timeout: 5))
        settle(1)
        all.tap()
        XCTAssertTrue(app.staticTexts["Stories"].firstMatch.waitForExistence(timeout: 5))
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
        let title = app.staticTexts["Stories"].firstMatch
        XCTAssertTrue(title.waitForExistence(timeout: 5))
        let top = title.frame.minY
        dragUp(app)
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
