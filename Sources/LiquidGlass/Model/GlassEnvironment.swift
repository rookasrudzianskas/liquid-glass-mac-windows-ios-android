import SwiftUI

// MARK: Public inherited defaults

private struct GlassMaterialKey: EnvironmentKey {
    static let defaultValue = GlassMaterial.clear
}

private struct GlassAppearanceKey: EnvironmentKey {
    static let defaultValue = GlassAppearance.automatic
}

private struct GlassRefractionKey: EnvironmentKey {
    static let defaultValue = GlassOptics.defaultRefraction
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

    /// The optical travel at the rim, in points, when a component doesn't set one.
    public var glassRefraction: Double {
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

    /// Sets the default refraction, in points of optical travel at the rim.
    public func glassRefraction(_ refraction: Double) -> some View {
        environment(\.glassRefraction, max(0, refraction))
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
}
