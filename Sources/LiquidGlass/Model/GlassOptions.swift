import SwiftUI

/// The optical material of a glass surface.
///
/// ``clear`` refracts the backdrop sharply with a light tone curve. ``regular``
/// frosts the backdrop (a σ 7.33 blur brightened or deepened by a wide fill
/// blur) before refracting it, which keeps text on the glass legible over busy
/// content. See `spec/OPTICS.md` §3.
public enum GlassMaterial: String, CaseIterable, Hashable, Sendable {
    /// Sharp, transparent glass.
    case clear
    /// Frosted glass, the default for menus and panels.
    case regular
}

/// Whether glass renders for a light or a dark backdrop.
///
/// The appearance selects the light or dark tone curves, the rim light
/// strength and the default foreground color (`#111113` light, white dark).
public enum GlassAppearance: String, CaseIterable, Hashable, Sendable {
    /// Follow the environment's color scheme.
    case automatic
    /// Always use the light material.
    case light
    /// Always use the dark material.
    case dark

    /// Resolves the appearance against a color scheme.
    /// - Returns: `true` when the dark material applies.
    public func isDark(in colorScheme: ColorScheme) -> Bool {
        switch self {
        case .automatic: colorScheme == .dark
        case .light: false
        case .dark: true
        }
    }
}

/// How much glass moves in response to touch and pointer input.
///
/// The system Reduce Motion setting always applies on top of this value: with
/// Reduce Motion on, ``full`` behaves like ``reduced``.
public enum GlassMotion: String, CaseIterable, Hashable, Sendable {
    /// Press grow, volume-preserving drag stretch and gel-like overshoot.
    case full
    /// No grow, stretch or overshoot; the light still fades in and out.
    case reduced
    /// No transitions at all: every change applies immediately.
    case none

    /// Combines the requested level with the system Reduce Motion setting.
    public func resolved(reduceMotion: Bool) -> GlassMotion {
        self == .full && reduceMotion ? .reduced : self
    }
}

/// The corner radius of a glass outline.
///
/// Numbers are corner radii in points and produce continuous ("squircle")
/// corners whose diagonal inset matches a circular corner of the same radius.
/// ``capsule`` and ``circle`` round the short sides fully. You can write a
/// number literal wherever a `GlassRadius` is expected:
///
/// ```swift
/// GlassSurface(radius: 24) { … }
/// GlassSurface(radius: .capsule) { … }
/// ```
public enum GlassRadius: Hashable, Sendable, ExpressibleByIntegerLiteral, ExpressibleByFloatLiteral {
    /// A continuous corner with this radius in points (nonnegative).
    case points(Double)
    /// Fully rounded short sides.
    case capsule
    /// Fully rounded; identical to ``capsule`` (a circle when the outline is square).
    case circle

    public init(integerLiteral value: Int) { self = .points(Double(value)) }
    public init(floatLiteral value: Double) { self = .points(value) }

    /// The radius as the shader encodes it: points, or `-1` for fully rounded.
    var shaderValue: Float {
        switch self {
        case .points(let value): Float(max(0, value))
        case .capsule, .circle: -1
        }
    }

    /// The radius of a shape inset by `inset` points, so nested outlines stay
    /// concentric with this one.
    func inset(by inset: Double) -> GlassRadius {
        switch self {
        case .points(let value): .points(max(0, value - inset))
        case .capsule, .circle: self
        }
    }
}
