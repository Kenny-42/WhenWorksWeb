using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="EventSettings.Description"/>'s validation attributes. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual <c>[StringLength]</c>/
/// <c>[RegularExpression]</c> attributes exactly as ASP.NET Core model binding would.
/// </summary>
public class EventSettingsTests
{
    private static EventSettings CreateValidEventSettings() => new() { EventId = 1, Emoji = ModelConstants.DefaultEventEmoji };

    private static bool IsDescriptionValid(string? candidate)
    {
        var context = new ValidationContext(CreateValidEventSettings()) { MemberName = nameof(EventSettings.Description) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
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
}
