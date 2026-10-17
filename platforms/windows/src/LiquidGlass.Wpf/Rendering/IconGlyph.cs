using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>
/// Recognizes icon-font glyphs: content that is only private-use characters, which is how icon fonts (Segoe Fluent
/// Icons, Segoe MDL2 Assets and most others) map their icons. Icon fonts have a single weight, so the glass styles draw
/// such content at normal weight; at SemiBold WPF would embolden the glyphs synthetically and smear them.
/// </summary>
internal sealed class IconGlyph : IValueConverter
{
    /// <summary>Converts content to whether it is an icon glyph (for style triggers).</summary>
    public static readonly IconGlyph Converter = new();

    /// <summary>Whether <paramref name="content"/> is a string (or a <see cref="TextBlock"/>) of private-use characters only.</summary>
    public static bool IsGlyph(object? content) => content switch
    {
        string text => IsGlyphText(text),
        TextBlock block => IsGlyphText(block.Text),
        _ => false,
    };

    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => IsGlyph(value);

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();

    private static bool IsGlyphText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var glyphs = 0;
        for (var i = 0; i < text!.Length; i++)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c)) continue;
            if (c >= '' && c <= '')
            {
                glyphs++;
                continue;
            }
            // Supplementary private-use planes (U+F0000 to U+10FFFF).
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) && char.ConvertToUtf32(c, text[i + 1]) >= 0xF0000)
            {
                glyphs++;
                i++;
                continue;
            }
            return false;
        }
        return glyphs > 0;
    }
}
