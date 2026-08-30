using System.Text;

namespace WhenWorksWeb.Services;

/// <summary>
/// Normalizes identity-bearing free-text fields (participant/user display names) to Unicode
/// Normalization Form C before they're persisted or compared, so two values that render
/// identically but differ only in codepoint composition (e.g. a precomposed "é" (U+00E9) vs. an
/// "e" followed by a combining acute accent (U+0065 U+0301)) are treated as the same value by
/// exact-match uniqueness checks such as <c>EventsController.SignIn.cs</c>'s
/// <c>ValidateParticipantUniquenessAsync</c>.
/// </summary>
public static class TextNormalizer
{
    /// <summary>
    /// Returns <paramref name="value"/> normalized to Unicode Normalization Form C, or
    /// <paramref name="value"/> unchanged if it's null, empty, or already normalized.
    /// </summary>
    /// <remarks>Callers are expected to trim before calling this, per the existing
    /// trim-then-validate convention elsewhere in the codebase; this method does not trim.</remarks>
    public static string? NormalizeToNfc(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.IsNormalized(NormalizationForm.FormC)
            ? value
            : value.Normalize(NormalizationForm.FormC);
    }
}
