using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="Event.Code"/> and <see cref="Event.Title"/>'s validation attributes. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual <c>[StringLength]</c>/
/// <c>[RegularExpression]</c> attributes exactly as ASP.NET Core model binding would.
/// </summary>
public class EventTests
{
    private static Event CreateValidEvent() => Event.Create("BCDFGH", "placeholder");

    private static bool IsPropertyValid<T>(string propertyName, T candidate)
    {
        var context = new ValidationContext(CreateValidEvent()) { MemberName = propertyName };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsCodeValid(string candidate) => IsPropertyValid(nameof(Event.Code), candidate);

    private static bool IsTitleValid(string candidate) => IsPropertyValid(nameof(Event.Title), candidate);

    [Theory]
    [InlineData("BCDFGH")]
    [InlineData("bcdfgh")]
    [InlineData("234567")]
    public void Code_AcceptsValidCodes(string code)
    {
        Assert.True(IsCodeValid(code));
    }

    [Theory]
    [InlineData("")] // empty — caught by StringLength's exact-length requirement
    [InlineData("AEILOU")] // excluded ambiguous letters
    [InlineData("BCDFG")] // too short
    [InlineData("BCDFGHJ")] // too long
    public void Code_RejectsInvalidCodes(string code)
    {
        // Note: unlike Participant.Color/ApplicationUser.DisplayName, a null Code isn't tested here — Code has
        // no [Required] attribute, but it's also never form-bound (only ever set via Event.Create with an
        // already-generated, already-valid code), and required on the C# property blocks null at compile
        // time. There's no reachable path for a null Code, so there's nothing to fix or usefully assert here.
        Assert.False(IsCodeValid(code));
    }

    [Theory]
    [InlineData("A")] // 1 character (MinimumLength boundary)
    public void Title_AcceptsNameWithinLengthBounds(string title)
    {
        Assert.True(IsTitleValid(title));
    }

    [Fact]
    public void Title_AcceptsExactlyMaxLength()
    {
        var thirtyChars = new string('A', ModelConstants.EventTitleMaxLength);

        Assert.True(IsTitleValid(thirtyChars));
    }

    [Fact]
    public void Title_RejectsEmptyString()
    {
        Assert.False(IsTitleValid(""));
    }

    [Fact]
    public void Title_RejectsNameLongerThanMaxLength()
    {
        var thirtyOneChars = new string('A', ModelConstants.EventTitleMaxLength + 1);

        Assert.False(IsTitleValid(thirtyOneChars));
    }

    [Theory]
    [InlineData("Trivia Night")]
    [InlineData("山田太郎")] // non-Latin script (Japanese) stays allowed
    [InlineData("O'Brien's Party!")] // ordinary punctuation stays allowed
    [InlineData(" Trivia Night")] // leading whitespace is allowed by the pattern -- trimmed by the caller before persisting
    [InlineData("Trivia Night ")] // trailing whitespace likewise allowed here, trimmed by the caller
    public void Title_AcceptsNamesWithNonAsciiOrPunctuation(string title)
    {
        Assert.True(IsTitleValid(title));
    }

    [Theory]
    [InlineData("   ")] // whitespace-only -- the gap [StringLength]'s MinimumLength alone doesn't close
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
}
