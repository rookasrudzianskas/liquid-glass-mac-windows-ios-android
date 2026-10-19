import SwiftUI

// MARK: Public inherited defaults

private struct GlassMaterialKey: EnvironmentKey {
    static let defaultValue = GlassMaterial.clear
}

private struct GlassAppearanceKey: EnvironmentKey {
    static let defaultValue = GlassAppearance.automatic
}

private struct GlassRefractionKey: EnvironmentKey {
    static let defaultValue: Double? = nil
}

private struct GlassMotionKey: EnvironmentKey {
    static let defaultValue = GlassMotion.full
}

extension EnvironmentValues {
    /// The material glass uses when a component doesn't set one.
    /// A ``GlassScene`` sets it for everything inside it; menus default to
    /// ``GlassMaterial/regular`` regardless.
    public var glassMaterial: GlassMaterial {
        get { self[GlassMaterialKey.self] }
        set { self[GlassMaterialKey.self] = newValue }
    }

    /// The appearance glass uses when a component doesn't set one.
    public var glassAppearance: GlassAppearance {
        get { self[GlassAppearanceKey.self] }
        set { self[GlassAppearanceKey.self] = newValue }
    }

    /// The optical travel at the rim, in points, of glass that doesn't set
    /// its own, at every size.
    ///
    /// `nil`, the default, lets each surface's resting size decide:
    /// ``GlassOptics/defaultRefraction(width:height:)`` gives large surfaces
    /// 60 points and small controls three quarters of their shorter side.
    public var glassRefraction: Double? {
        get { self[GlassRefractionKey.self] }
        set { self[GlassRefractionKey.self] = newValue }
    }

    /// How much glass moves in response to input. Reduce Motion always applies.
    public var glassMotion: GlassMotion {
        get { self[GlassMotionKey.self] }
        set { self[GlassMotionKey.self] = newValue }
    }
}

extension View {
    /// Sets the default material for glass inside this view.
    public func glassMaterial(_ material: GlassMaterial) -> some View {
        environment(\.glassMaterial, material)
    }

    /// Sets the default appearance for glass inside this view.
    public func glassAppearance(_ appearance: GlassAppearance) -> some View {
        environment(\.glassAppearance, appearance)
    }

    /// Sets the refraction, in points of optical travel at the rim, of glass
    /// inside this view that doesn't set its own. It applies at every size;
    /// `nil` restores the default, which follows each surface's resting size
    /// (``GlassOptics/defaultRefraction(width:height:)``).
    public func glassRefraction(_ refraction: Double?) -> some View {
        environment(\.glassRefraction, refraction.map { max(0, $0) })
    }

    /// Sets how much glass inside this view moves in response to input.
    public func glassMotion(_ motion: GlassMotion) -> some View {
        environment(\.glassMotion, motion)
    }
}

// MARK: Internal plumbing

/// Where a view is rendered relative to its scene's backdrop.
enum GlassBackdropRole: Hashable {
    /// Not inside a scene's backdrop.
    case none
    /// The visible, interactive backdrop.
    case primary
    /// A copy rendered inside a glass layer; never interactive.
    case copy
}

private struct GlassBackdropKey: EnvironmentKey {
    static let defaultValue: GlassBackdrop? = nil
}

private struct GlassBackdropRoleKey: EnvironmentKey {
    static let defaultValue = GlassBackdropRole.none
}

private struct GlassNestingKey: EnvironmentKey {
    static let defaultValue = false
}

private struct GlassToolbarItemKey: EnvironmentKey {
    static let defaultValue = false
}

private struct GlassMenuDestructiveKey: EnvironmentKey {
    static let defaultValue = GlassForeground.destructive(dark: false)
}

private struct GlassOverlayAvailableKey: EnvironmentKey {
    static let defaultValue = false
}

extension EnvironmentValues {
    /// The backdrop of the enclosing scene, available to its foreground.
    var glassBackdrop: GlassBackdrop? {
        get { self[GlassBackdropKey.self] }
        set { self[GlassBackdropKey.self] = newValue }
    }

    var glassBackdropRole: GlassBackdropRole {
        get { self[GlassBackdropRoleKey.self] }
        set { self[GlassBackdropRoleKey.self] = newValue }
    }

    /// Whether the view sits on another glass surface.
    var glassNested: Bool {
        get { self[GlassNestingKey.self] }
        set { self[GlassNestingKey.self] = newValue }
    }

    /// Whether buttons render as toolbar items (no glass of their own).
    var glassToolbarItem: Bool {
        get { self[GlassToolbarItemKey.self] }
        set { self[GlassToolbarItemKey.self] = newValue }
    }

    /// The color of a destructive menu item drawn over this glass
    /// (``GlassOptics/menuDestructive(dark:)`` on the side of its labels).
    var glassMenuDestructive: Color {
        get { self[GlassMenuDestructiveKey.self] }
        set { self[GlassMenuDestructiveKey.self] = newValue }
    }

    /// Whether a scene above can present glass popups such as menus.
    var glassOverlayAvailable: Bool {
        get { self[GlassOverlayAvailableKey.self] }
        set { self[GlassOverlayAvailableKey.self] = newValue }
    }
}

/// The foreground color of text and icons on glass (`spec/OPTICS.md` §8).
enum GlassForeground {
    static let light = Color(.sRGB, red: 17 / 255, green: 17 / 255, blue: 19 / 255)
    static let dark = Color.white

    static func color(dark: Bool) -> Color { dark ? Self.dark : Self.light }

    /// A destructive menu item's red (``GlassOptics/menuDestructive(dark:)``):
    /// `#FFEEED` where the labels are white, `#560D0A` where they are dark.
    static func destructive(dark: Bool) -> Color {
        let c = GlassOptics.menuDestructive(dark: dark)
        return Color(.sRGB, red: c.x, green: c.y, blue: c.z)
    }

    /// The label color on glass tinted with `tint` (gamma-encoded sRGB):
    /// white or the light glass's `#111113`, whichever has the higher WCAG
    /// contrast with the glass as the tint formula draws it over a mid-tone
    /// backdrop (`spec/OPTICS.md` §3: the pigment passes
    /// `body + (1 − body)·luma`, with luma 0.5). Blue, red or purple glass
    /// takes white; yellow, mint, green, orange or cyan glass a dark label.
    ///
    /// Glass mixed toward the legibility backing (menu panels) passes the
    /// full amount, ``GlassOptics/menuLegibility``, as `legibility`, and the
    /// glass compared is the tint mixed toward the backing of `dark`
    /// appearance (``GlassOptics/menuForeground(tint:material:dark:legibility:)``),
    /// so the label keeps its color while the panel opens and closes.
    static func onTint(_ tint: SIMD3<Float>, material: GlassMaterial, dark: Bool = false, legibility: Double = 0) -> Color {
        let label = GlassOptics.menuForeground(
            tint: SIMD3(Double(tint.x), Double(tint.y), Double(tint.z)), material: material, dark: dark, legibility: legibility)
        return label.x > 0.5 ? Self.dark : Self.light
    }

    /// WCAG 2 relative luminance of a gamma-encoded sRGB color.
    static func relativeLuminance(_ red: Double, _ green: Double, _ blue: Double) -> Double {
        GlassOptics.relativeLuminance(SIMD3(red, green, blue))
    }
}
