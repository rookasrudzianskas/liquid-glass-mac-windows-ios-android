// Prints the window number of the largest window owned by a process.
// Usage: swift window-id.swift <pid>
import CoreGraphics
import Foundation

let pid = Int32(CommandLine.arguments[1]) ?? 0
let windows = CGWindowListCopyWindowInfo([.optionAll], kCGNullWindowID) as? [[String: Any]] ?? []
let owned = windows.filter { ($0[kCGWindowOwnerPID as String] as? Int32) == pid && ($0[kCGWindowLayer as String] as? Int) == 0 }
let area: ([String: Any]) -> Double = { window in
    let bounds = window[kCGWindowBounds as String] as? [String: Double] ?? [:]
    return (bounds["Width"] ?? 0) * (bounds["Height"] ?? 0)
}
if let window = owned.max(by: { area($0) < area($1) }), let number = window[kCGWindowNumber as String] as? Int {
    print(number)
} else {
    exit(1)
}
