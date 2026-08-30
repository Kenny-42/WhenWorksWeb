using System.Text.RegularExpressions;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Tests.Common;

/// <summary>
/// Tier 1 unit tests for internal coherence between the constants in <see cref="ModelConstants"/> — e.g.
/// that <see cref="ModelConstants.EventCodePattern"/>'s character class describes exactly the same set of
/// characters as <see cref="ModelConstants.UniqueCodeAlphabet"/>. These are properties of the constants
/// themselves, independent of how any particular model consumes them via <c>[RegularExpression]</c> — see
/// <c>IndexViewModelTests</c> and <c>ParticipantTests</c> for tests of the actual DataAnnotations validation
/// behavior those patterns drive.
/// </summary>
public class ModelConstantsTests
{
    [Theory]
    [InlineData("A", false)]
    [InlineData("E", false)]
    [InlineData("I", false)]
    [InlineData("L", false)]
    [InlineData("O", false)]
    [InlineData("U", false)]
    [InlineData("0", false)]
    [InlineData("1", false)]
    public void UniqueCodeAlphabet_ExcludesAmbiguousCharacters(string character, bool expectedPresent)
    {
        Assert.Equal(expectedPresent, ModelConstants.UniqueCodeAlphabet.Contains(character, StringComparison.Ordinal));
    }

    [Fact]
    public void UniqueCodeAlphabet_HasNoDuplicateCharacters()
    {
        var distinctCount = ModelConstants.UniqueCodeAlphabet.Distinct().Count();

        Assert.Equal(ModelConstants.UniqueCodeAlphabet.Length, distinctCount);
    }

    [Fact]
    public void UniqueCodeAlphabet_LengthMatchesEventCodePatternCharacterClassCount()
    {
        // Guards against someone adding/removing a character in one constant without updating the other —
        // the two are meant to describe exactly the same set of characters.
        var characterClassMatch = Regex.Match(ModelConstants.EventCodePattern, @"\[([^\]]+)\]");
        Assert.True(characterClassMatch.Success, "Expected EventCodePattern to contain a single character class.");

        var charactersInPattern = characterClassMatch.Groups[1].Value;
        Assert.Equal(ModelConstants.UniqueCodeAlphabet.Length, charactersInPattern.Length);

        foreach (var c in ModelConstants.UniqueCodeAlphabet)
        {
            Assert.Contains(c, charactersInPattern);
        }
    }

    [Fact]
    public void DefaultParticipantColor_HasHexColorLength()
    {
        Assert.Equal(ModelConstants.HexColorLength, ModelConstants.DefaultParticipantColor.Length);
    }

    [Fact]
    public void DefaultEventEmoji_DoesNotExceedEventEmojiMaxLength()
    {
        Assert.True(ModelConstants.DefaultEventEmoji.Length <= ModelConstants.EventEmojiMaxLength);
    }

    [Fact]
    public void PasswordMinLength_IsNotGreaterThanPasswordMaxLength()
    {
        Assert.True(ModelConstants.PasswordMinLength <= ModelConstants.PasswordMaxLength);
    }

    // PasswordComplexityPattern/PhoneNumberPattern/DisplayNameContentPattern are exercised through
    // the real InputModel classes that use them in ChangePasswordModelInputTests,
    // SetPasswordModelInputTests, and IndexModelInputTests (per CODING_CONVENTIONS.md's guidance to
    // test DataAnnotations-validated properties through the real model, not the raw pattern). The
    // checks below are properties of the constants themselves -- that they don't rely on \p{}
    // Unicode property escapes, which would throw client-side (see each constant's own remarks).
    [Theory]
    [InlineData(ModelConstants.PasswordComplexityPattern)]
    [InlineData(ModelConstants.DisplayNameContentPattern)]
    public void ClientCompatiblePatterns_DoNotUseUnicodePropertyEscapes(string pattern)
    {
        Assert.DoesNotContain(@"\p{", pattern, StringComparison.Ordinal);
    }
}
