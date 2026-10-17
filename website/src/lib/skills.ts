// What each agent skill is for, written for people. A SKILL.md description is
// written for agents (it lists trigger words), so the pages lead with these and
// keep the description behind a disclosure.

export const SKILL_ROLES: Record<string, string> = {
  "liquid-glass": "Design and platform choice",
  "liquid-glass-swiftui": "SwiftUI on iOS and macOS",
  "liquid-glass-compose": "Jetpack Compose on Android",
  "liquid-glass-wpf": "WPF on Windows",
  "liquid-glass-port": "Port the optical model",
};

export const SKILL_SUMMARIES: Record<string, string> = {
  "liquid-glass": "Where glass belongs, when to use Clear or Regular, and which library fits each platform. The design rules the other skills apply.",
  "liquid-glass-swiftui": "Writes SwiftUI glass with the LiquidGlass package: scenes, surfaces and every control, for iOS 17+ and macOS 14+, and fixes glass that shows nothing or only a rim.",
  "liquid-glass-compose": "Writes Jetpack Compose glass with liquid-glass-compose: scenes, surfaces, Modifier.liquidGlass and the controls, with the fallback material below Android 13.",
  "liquid-glass-wpf": "Writes WPF glass in XAML or C# with LiquidGlassEngineering.Wpf: scenes, surfaces and the controls, on .NET 8+ and .NET Framework 4.8.",
  "liquid-glass-port": "Ports the optical model to a new renderer, such as Flutter, Unity, Godot or WebGPU, and checks it against the golden test vectors.",
};
