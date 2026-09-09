using System.Text.RegularExpressions;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Pulls the antiforgery hidden field's value out of a rendered form's HTML, for Tier 3 tests that need to
/// submit a real POST through the actual antiforgery pipeline rather than bypassing it.
/// </summary>
public static class AntiForgeryTokenExtractor
{
    /// <summary>
    /// Extracts the <c>__RequestVerificationToken</c> value from the given page HTML. Locates the whole
    /// <c>&lt;input&gt;</c> tag first so it isn't sensitive to attribute order (ASP.NET Core's FormTagHelper
    /// doesn't guarantee <c>name</c> comes before <c>value</c>).
    /// </summary>
    public static string ExtractRequestVerificationToken(string html)
    {
        var inputTag = Regex.Match(html, @"<input[^>]*__RequestVerificationToken[^>]*>");
        if (!inputTag.Success)
        {
            throw new InvalidOperationException("No __RequestVerificationToken input found in the given HTML.");
        }

        var value = Regex.Match(inputTag.Value, @"value=""([^""]*)""");
        if (!value.Success)
        {
            throw new InvalidOperationException("__RequestVerificationToken input had no value attribute.");
        }

        return value.Groups[1].Value;
    }
}
