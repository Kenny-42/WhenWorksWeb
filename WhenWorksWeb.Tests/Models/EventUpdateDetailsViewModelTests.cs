using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="EventUpdateDetailsViewModel"/>'s validation attributes. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual attributes exactly as ASP.NET
/// Core model binding would, rather than reimplementing their match semantics by hand — see
/// CODING_CONVENTIONS.md's Testing Conventions section for why.
/// </summary>
public class EventUpdateDetailsViewModelTests
{
    private static bool IsTitleValid(string? candidate)
    {
        var model = new EventUpdateDetailsViewModel();
        var context = new ValidationContext(model) { MemberName = nameof(EventUpdateDetailsViewModel.Title) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsDescriptionValid(string? candidate)
    {
        var model = new EventUpdateDetailsViewModel { Title = "placeholder" };
        var context = new ValidationContext(model) { MemberName = nameof(EventUpdateDetailsViewModel.Description) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsEmojiValid(string? candidate)
    {
        var model = new EventUpdateDetailsViewModel { Title = "placeholder" };
        var context = new ValidationContext(model) { MemberName = nameof(EventUpdateDetailsViewModel.Emoji) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Trivia Night")]
    public void Title_AcceptsNonEmptyValueWithinMaxLength(string title)
    {
        Assert.True(IsTitleValid(title));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("")] // empty
    public void Title_RejectsMissingOrEmptyValue(string? title)
    {
        Assert.False(IsTitleValid(title));
    }

    [Fact]
    public void Title_RejectsValueOverMaxLength()
    {
        var tooLong = new string('a', ModelConstants.EventTitleMaxLength + 1);
        Assert.False(IsTitleValid(tooLong));
    }

    [Fact]
    public void Title_AcceptsValueAtMaxLength()
    {
        var atMax = new string('a', ModelConstants.EventTitleMaxLength);
        Assert.True(IsTitleValid(atMax));
    }

    [Theory]
    [InlineData("山田太郎")] // non-Latin script (Japanese) stays allowed
    [InlineData("O'Brien's Party!")] // ordinary punctuation stays allowed
    [InlineData(" Trivia Night")] // leading whitespace is allowed by the pattern -- trimmed by the caller before persisting
    [InlineData("Trivia Night ")] // trailing whitespace likewise allowed here, trimmed by the caller
    public void Title_AcceptsNamesWithNonAsciiOrPunctuation(string title)
    {
        Assert.True(IsTitleValid(title));
    }

    [Theory]
    [InlineData("   ")] // whitespace-only -- the gap [Required]/[StringLength] alone doesn't close
    [InlineData("\t\t")] // whitespace-only (tabs)
    public void Title_RejectsWhitespaceOnlyValue(string title)
    {
        Assert.False(IsTitleValid(title));
    }

    // A representative sample, not the full character class -- DisplayNameContentPattern's
    // complete C0/C1/zero-width coverage is exhaustively proven once, against
    // Participant.DisplayName, in ParticipantTests.DisplayName_RejectsControlAndInvisibleCharacters.
    // These exist only to confirm the attribute is actually wired up on this property.
    [Theory]
    [InlineData("Trivia\u0000Night")] // embedded NUL control character (C0)
    [InlineData("Trivia\u007FNight")] // embedded DEL control character (C1 boundary)
    [InlineData("Trivia\u200BNight")] // embedded zero-width space
    public void Title_RejectsControlAndInvisibleCharacters(string title)
    {
        Assert.False(IsTitleValid(title));
    }

    [Fact]
    public void Description_AcceptsNullOrEmpty()
    {
        Assert.True(IsDescriptionValid(null));
        Assert.True(IsDescriptionValid(""));
    }

    [Fact]
    public void Description_RejectsValueOverMaxLength()
    {
        var tooLong = new string('a', ModelConstants.EventDescriptionMaxLength + 1);
        Assert.False(IsDescriptionValid(tooLong));
    }

    [Fact]
    public void Description_AcceptsValueAtMaxLength()
    {
        var atMax = new string('a', ModelConstants.EventDescriptionMaxLength);
        Assert.True(IsDescriptionValid(atMax));
    }

    [Theory]
    [InlineData("Line one\nLine two")] // embedded LF is explicitly allowed, unlike Title/DisplayName
    [InlineData("Line one\r\nLine two")] // embedded CRLF is likewise allowed
    [InlineData("\nLeading newline")]
    [InlineData("Trailing newline\n")]
    [InlineData("山田太郎")] // non-Latin script (Japanese) stays allowed
    [InlineData("Plans, plans, plans!")] // ordinary punctuation stays allowed
    [InlineData(" Leading space")] // leading whitespace is allowed by the pattern -- trimmed by the caller before persisting
    [InlineData("Trailing space ")] // trailing whitespace likewise allowed here, trimmed by the caller
    public void Description_AcceptsMultilineAndNonAsciiContent(string description)
    {
        Assert.True(IsDescriptionValid(description));
    }

    [Theory]
    [InlineData("\n")] // newline-only -- no non-whitespace character present
    [InlineData("   \n   ")] // whitespace and a newline, still nothing non-whitespace
    [InlineData("\t\t")] // whitespace-only (tabs) -- no embedded newline involved at all
    public void Description_RejectsWhitespaceOnlyValue(string description)
    {
        Assert.False(IsDescriptionValid(description));
    }

    [Theory]
    [InlineData("Line one\nLine\u0000two")] // control character on the second line -- the exact case
                                             // DescriptionContentPattern's [\s\S] rewrite exists to
                                             // still catch, since a plain ".*[bad chars]" lookahead
                                             // stops scanning at the first newline
    [InlineData("Line\u0000one\nLine two")] // control character on the first line (would already be
                                             // caught by a naive pattern too, kept as a baseline)
    [InlineData("Line one\nLine\u200Btwo")] // zero-width space on the second line
    [InlineData("Line one\nLine\tTabbed")] // an embedded raw tab is still blocked even in the
                                            // multiline pattern -- only \n/\r are exempted
    public void Description_RejectsControlAndInvisibleCharactersAcrossLines(string description)
    {
        Assert.False(IsDescriptionValid(description));
    }

    [Fact]
    public void Emoji_AcceptsNullOrEmpty()
    {
        Assert.True(IsEmojiValid(null));
        Assert.True(IsEmojiValid(""));
    }

    [Fact]
    public void Emoji_AcceptsSingleCodepointEmoji()
    {
        Assert.True(IsEmojiValid("🎲"));
    }

    [Theory]
    [InlineData("🎉🎉")]
    [InlineData("hi")]
    public void Emoji_RejectsMultiCharacterValue(string invalid)
    {
        Assert.False(IsEmojiValid(invalid));
    }
}
