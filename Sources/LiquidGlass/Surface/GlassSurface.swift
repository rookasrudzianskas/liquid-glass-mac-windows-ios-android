import SwiftUI

/// A glass container.
///
/// The surface refracts its ``GlassScene``'s backdrop through a continuous-
/// corner outline with a 20-point bevel, tones it with the chosen material,
/// and paints the rim light over it. Its content draws above the glass, is
/// clipped to the outline and is not refracted.
///
/// ```swift
/// GlassSurface(material: .regular, radius: 28) {
///     VStack(alignment: .leading) {
///         Text("Liquid Glass").font(.title.bold())
///         Text("Refraction, frost and light.")
///     }
///     .padding(24)
/// }
/// ```
///
/// Interactive surfaces grow under a press, stretch toward a drag with
/// constant volume, settle with a gel-like overshoot and light up under the
/// finger or pointer. Outside a scene the surface falls back to the platform
/// material with the same rim light.
public struct GlassSurface<Content: View>: View {
    private let options: GlassSurfaceOptions
    private let content: Content

    /// Creates a glass surface.
    /// - Parameters:
    ///   - material: Clear or Regular glass; `nil` inherits the scene's material.
    ///   - appearance: Light or dark glass; `nil` inherits.
    ///   - radius: The corner radius, `.capsule` or `.circle`. Default 16.
    ///   - refraction: Optical travel at the rim in points; `nil` inherits (60).
    ///   - tint: Colors the glass, never the content.
    ///   - interactive: Press grow, drag stretch and glow.
    ///   - motion: Overrides the scene's motion level.
    ///   - content: The content drawn on the glass.
    public init(
        material: GlassMaterial? = nil,
        appearance: GlassAppearance? = nil,
        radius: GlassRadius = 16,
        refraction: Double? = nil,
        tint: Color? = nil,
        interactive: Bool = false,
        motion: GlassMotion? = nil,
        @ViewBuilder content: () -> Content
    ) {
        self.options = GlassSurfaceOptions(
            material: material, appearance: appearance, radius: radius, refraction: refraction,
            tint: tint, interactive: interactive, motion: motion
        )
        self.content = content()
    }

    public var body: some View {
        GlassSurfaceCore(options: options, content: content)
            .accessibilityElement(children: .contain)
    }
}

extension View {
    /// Places this view on a glass surface.
    ///
    /// Equivalent to wrapping the view in a ``GlassSurface`` with the same
    /// options:
    ///
    /// ```swift
    /// Label("Now Playing", systemImage: "music.note")
    ///     .padding()
    ///     .liquidGlass(.regular, radius: .capsule, interactive: true)
    /// ```
    public func liquidGlass(
        _ material: GlassMaterial? = nil,
        appearance: GlassAppearance? = nil,
        radius: GlassRadius = 16,
        refraction: Double? = nil,
        tint: Color? = nil,
        interactive: Bool = false,
        motion: GlassMotion? = nil
    ) -> some View {
        GlassSurface(
            material: material, appearance: appearance, radius: radius, refraction: refraction,
            tint: tint, interactive: interactive, motion: motion
        ) { self }
    }
}
