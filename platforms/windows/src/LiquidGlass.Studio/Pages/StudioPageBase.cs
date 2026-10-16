using System.Windows.Controls;

namespace LiquidGlass.Studio.Pages;

/// <summary>A demo page; pages pick wallpapers that suit light or dark glass.</summary>
public abstract class StudioPageBase : UserControl
{
    /// <summary>Called when the window's resolved appearance changes.</summary>
    public abstract void SetDark(bool dark);
}
