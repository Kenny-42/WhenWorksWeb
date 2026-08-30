using WhenWorksWeb.Services;

namespace WhenWorksWeb.Tests.Services;

/// <summary>
/// Tier 1 unit tests for <see cref="TextNormalizer.NormalizeToNfc"/>, added by
/// Spec/Features/FEATURES-tighten-input-validation-site-wide.ospec Section 3 (Issue #88). No
/// database or HTTP context involved.
/// </summary>
public class TextNormalizerTests
{
    /// <summary>
    /// "e with acute accent" as a single precomposed codepoint (U+00E9) -- the canonical NFC form.
    /// Written as an explicit \u escape (rather than a literal accented character in source) so the
    /// exact codepoint sequence doesn't depend on how the source file itself happens to be
    /// encoded/normalized.
    /// </summary>
    private const string PrecomposedEAcute = "é";

    /// <summary>
    /// The same "e with acute accent" as "e" (U+0065) followed by a combining acute accent
    /// (U+0301) -- the canonical NFD form. Renders identically to <see cref="PrecomposedEAcute"/>
    /// but compares unequal to it under ordinal/culture string comparison without normalization.
    /// </summary>
    private const string DecomposedEAcute = "é";

    /// <summary>The Hangul syllable "ga" as a single precomposed codepoint (U+AC00).</summary>
    private const string PrecomposedHangul = "가";

    /// <summary>
    /// The same Hangul syllable decomposed into its leading consonant jamo (U+1100) and vowel jamo
    /// (U+1161) -- confirms the helper isn't scoped to Latin-with-diacritics text only.
    /// </summary>
    private const string DecomposedHangul = "가";

    [Fact]
    public void NormalizeToNfc_WithNull_ReturnsNull()
    {
        Assert.Null(TextNormalizer.NormalizeToNfc(null));
    }

    [Fact]
    public void NormalizeToNfc_WithEmptyString_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, TextNormalizer.NormalizeToNfc(string.Empty));
    }

    [Fact]
    public void NormalizeToNfc_WithPlainAsciiText_ReturnsSameValue()
    {
        Assert.Equal("Alice", TextNormalizer.NormalizeToNfc("Alice"));
    }

    [Fact]
    public void NormalizeToNfc_WithAlreadyComposedText_ReturnsSameValue()
    {
        var input = "Caf" + PrecomposedEAcute;

        Assert.Equal(input, TextNormalizer.NormalizeToNfc(input), StringComparer.Ordinal);
    }

    [Fact]
    public void NormalizeToNfc_WithDecomposedText_ReturnsComposedValue()
    {
        var decomposed = "Caf" + DecomposedEAcute;
        var composed = "Caf" + PrecomposedEAcute;

        Assert.Equal(composed, TextNormalizer.NormalizeToNfc(decomposed), StringComparer.Ordinal);
    }

    /// <summary>
    /// The whole point of this helper: two strings that render identically but differ only in
    /// codepoint composition must compare equal once both are normalized.
    /// </summary>
    [Fact]
    public void NormalizeToNfc_ComposedAndDecomposedVariantsOfSameText_ProduceEqualResults()
    {
        var composedInput = "Caf" + PrecomposedEAcute;
        var decomposedInput = "Caf" + DecomposedEAcute;

        // Sanity check that the two inputs actually differ before normalization -- otherwise this
        // test wouldn't be exercising anything.
        Assert.NotEqual(composedInput, decomposedInput, StringComparer.Ordinal);

        Assert.Equal(
            TextNormalizer.NormalizeToNfc(composedInput),
            TextNormalizer.NormalizeToNfc(decomposedInput),
            StringComparer.Ordinal);
    }

    [Fact]
    public void NormalizeToNfc_IsIdempotent()
    {
        var decomposed = "Caf" + DecomposedEAcute;

        var normalizedOnce = TextNormalizer.NormalizeToNfc(decomposed);
        var normalizedTwice = TextNormalizer.NormalizeToNfc(normalizedOnce);

        Assert.Equal(normalizedOnce, normalizedTwice, StringComparer.Ordinal);
    }

    [Fact]
    public void NormalizeToNfc_WithNonLatinScript_ReturnsSameComposedValue()
    {
        Assert.NotEqual(PrecomposedHangul, DecomposedHangul, StringComparer.Ordinal);
        Assert.Equal(
            TextNormalizer.NormalizeToNfc(PrecomposedHangul),
            TextNormalizer.NormalizeToNfc(DecomposedHangul),
            StringComparer.Ordinal);
    }

    [Fact]
    public void NormalizeToNfc_WithWhitespaceOnlyString_ReturnsSameValue()
    {
        Assert.Equal("   ", TextNormalizer.NormalizeToNfc("   "));
    }

    [Fact]
    public void NormalizeToNfc_DoesNotTrim()
    {
        var input = "  Caf" + DecomposedEAcute + "  ";

        var result = TextNormalizer.NormalizeToNfc(input);

        Assert.StartsWith("  ", result, StringComparison.Ordinal);
        Assert.EndsWith("  ", result, StringComparison.Ordinal);
    }
}
