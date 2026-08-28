namespace WhenWorksWeb.Services;

/// <summary>
/// Picks whichever of white or the site's dark "strong" text color reads better against a
/// given background, via the WCAG 2 relative-luminance contrast formula rather than a fixed
/// brightness threshold — stays correct across the full range of user-picked colors.
/// </summary>
public static class ColorContrastHelper
{
    // Mirrors --color-text-strong in wwwroot/css/site.css. Kept as a literal here (nothing
    // else in this project reads a CSS custom property's value back into C#) — if that
    // token's value ever changes, update this to match.
    private const string DarkTextColor = "#514c54";
    private const string LightTextColor = "#ffffff";

    /// <summary>
    /// Returns whichever of <see cref="LightTextColor"/> or <see cref="DarkTextColor"/> has the
    /// higher WCAG contrast ratio against <paramref name="hexColor"/>.
    /// </summary>
    /// <param name="hexColor">A 6-digit hex color, with or without a leading '#'.</param>
public static string GetReadableTextColor(string hexColor)
{
    try
    {
        var (r, g, b) = ParseHex(hexColor);
        var backgroundLuminance = RelativeLuminance(r, g, b);

        var contrastWithDark = ContrastRatio(backgroundLuminance, RelativeLuminance(0x51, 0x4c, 0x54));
        var contrastWithLight = ContrastRatio(backgroundLuminance, RelativeLuminance(0xff, 0xff, 0xff));

        return contrastWithLight >= contrastWithDark ? LightTextColor : DarkTextColor;
    }
    catch (ArgumentOutOfRangeException)
    {
        return DarkTextColor;
    }
    catch (FormatException)
    {
        return DarkTextColor;
    }
}

    private static (int R, int G, int B) ParseHex(string hexColor)
    {
        var hex = hexColor.AsSpan().TrimStart('#');
        var r = Convert.ToInt32(hex[..2].ToString(), 16);
        var g = Convert.ToInt32(hex[2..4].ToString(), 16);
        var b = Convert.ToInt32(hex[4..6].ToString(), 16);
        return (r, g, b);
    }

    // https://www.w3.org/TR/WCAG21/#dfn-relative-luminance
    private static double RelativeLuminance(int r, int g, int b)
    {
        return 0.2126 * Linearize(r) + 0.7152 * Linearize(g) + 0.0722 * Linearize(b);

        static double Linearize(int channel)
        {
            var c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }

    // https://www.w3.org/TR/WCAG21/#dfn-contrast-ratio — (lighter + 0.05) / (darker + 0.05).
    private static double ContrastRatio(double luminanceA, double luminanceB)
    {
        var lighter = Math.Max(luminanceA, luminanceB);
        var darker = Math.Min(luminanceA, luminanceB);
        return (lighter + 0.05) / (darker + 0.05);
    }
}
