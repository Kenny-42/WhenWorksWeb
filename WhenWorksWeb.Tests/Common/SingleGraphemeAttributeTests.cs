using WhenWorksWeb.Common;

namespace WhenWorksWeb.Tests.Common;

/// <summary>
/// Tier 1 unit tests for <see cref="SingleGraphemeAttribute.IsSingleGrapheme"/> — exercised
/// directly via the <c>internal</c> seam (see CODING_CONVENTIONS.md's Testing Conventions section
/// on when a small internal seam is worth adding) since a single grapheme cluster is a precise,
/// self-contained contract worth testing on its own, separate from the DataAnnotations plumbing
/// covered by <c>EventUpdateDetailsViewModelTests.Emoji_*</c>.
/// </summary>
public class SingleGraphemeAttributeTests
{
    [Fact]
    public void IsSingleGrapheme_WithSingleCodepointEmoji_ReturnsTrue()
    {
        Assert.True(SingleGraphemeAttribute.IsSingleGrapheme("🎲"));
    }

    [Fact]
    public void IsSingleGrapheme_WithSingleAsciiLetter_ReturnsTrue()
    {
        Assert.True(SingleGraphemeAttribute.IsSingleGrapheme("A"));
    }

    /// <summary>A skin-tone modifier sequence (thumbs up + medium skin tone) is two codepoints
    /// but one visible grapheme cluster.</summary>
    [Fact]
    public void IsSingleGrapheme_WithSkinToneModifierSequence_ReturnsTrue()
    {
        var thumbsUpMediumSkinTone = string.Concat("\U0001F44D", "\U0001F3FD");
        Assert.True(SingleGraphemeAttribute.IsSingleGrapheme(thumbsUpMediumSkinTone));
    }

    /// <summary>The family emoji (man+ZWJ+woman+ZWJ+girl+ZWJ+boy) is seven codepoints joined by
    /// zero-width joiners into one visible grapheme cluster — the case that motivated stripping
    /// ZWJ out before the invisible-character check rather than blanket-rejecting it.</summary>
    [Fact]
    public void IsSingleGrapheme_WithZwjFamilySequence_ReturnsTrue()
    {
        const string zwj = "\u200D";
        var family = string.Concat("\U0001F468", zwj, "\U0001F469", zwj, "\U0001F467", zwj, "\U0001F466");
        Assert.True(SingleGraphemeAttribute.IsSingleGrapheme(family));
    }

    [Theory]
    [InlineData("🎉🎉")] // two separate grapheme clusters
    [InlineData("hi")] // two separate grapheme clusters
    public void IsSingleGrapheme_WithMultipleGraphemeClusters_ReturnsFalse(string multiple)
    {
        Assert.False(SingleGraphemeAttribute.IsSingleGrapheme(multiple));
    }

    [Fact]
    public void IsSingleGrapheme_WithLoneZeroWidthJoiner_ReturnsFalse()
    {
        // Not attached to any real content either side — stripping the ZWJ leaves nothing behind,
        // so this must not slip through as "one grapheme cluster."
        Assert.False(SingleGraphemeAttribute.IsSingleGrapheme("\u200D"));
    }

    [Theory]
    [InlineData("\u200B")] // zero-width space
    [InlineData("\u200C")] // zero-width non-joiner
    [InlineData("\uFEFF")] // BOM
    [InlineData("\x01")] // C0 control character
    public void IsSingleGrapheme_WithInvisibleOrControlCharacterAlone_ReturnsFalse(string invisible)
    {
        Assert.False(SingleGraphemeAttribute.IsSingleGrapheme(invisible));
    }

    [Fact]
    public void IsSingleGrapheme_WithControlCharacterAlongsideRealContent_ReturnsFalse()
    {
        Assert.False(SingleGraphemeAttribute.IsSingleGrapheme("🎲\x01"));
    }

    [Fact]
    public void IsSingleGrapheme_WithWhitespaceOnly_ReturnsFalse()
    {
        Assert.False(SingleGraphemeAttribute.IsSingleGrapheme(" "));
    }
}
