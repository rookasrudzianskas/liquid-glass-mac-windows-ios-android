using System.Windows;
using System.Windows.Input;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>
/// Maintains <see cref="GlassScene.IsFocusVisibleProperty"/>: true while a control has keyboard focus that arrived
/// from the keyboard, so glass controls show their focus ring for keyboard users only, like Windows' own focus visuals.
/// </summary>
internal static class GlassFocus
{
    public static void Track(UIElement element)
    {
        element.GotKeyboardFocus += (_, e) =>
        {
            if (ReferenceEquals(e.NewFocus, element))
                GlassScene.SetIsFocusVisible(element, InputManager.Current.MostRecentInputDevice is KeyboardDevice);
        };
        element.LostKeyboardFocus += (_, e) =>
        {
            if (ReferenceEquals(e.OldFocus, element)) GlassScene.SetIsFocusVisible(element, false);
        };
        element.PreviewMouseDown += (_, _) => GlassScene.SetIsFocusVisible(element, false);
        element.KeyDown += (_, e) =>
        {
            if (element.IsKeyboardFocused && e.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down)
                GlassScene.SetIsFocusVisible(element, true);
        };
    }
}
