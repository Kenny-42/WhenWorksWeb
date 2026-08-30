using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="EventSignInViewModel"/>'s validation attributes — the actual form-bound
/// model behind the sign-in POST, so these boundaries are the most directly security-relevant in the app.
/// Uses <see cref="Validator.TryValidateProperty"/> to run the real attributes as ASP.NET Core model binding
/// would.
/// </summary>
public class EventSignInViewModelTests
{
    private static EventSignInViewModel CreateValidModel() => new() { Code = "BCDFGH" };

    private static bool IsPropertyValid<T>(string propertyName, T candidate)
    {
        var context = new ValidationContext(CreateValidModel()) { MemberName = propertyName };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsDisplayNameValid(string candidate) => IsPropertyValid(nameof(EventSignInViewModel.DisplayName), candidate);

    private static bool IsColorValid(string candidate) => IsPropertyValid(nameof(EventSignInViewModel.Color), candidate);

    [Fact]
    public void DisplayName_RejectsEmptyString()
    {
        Assert.False(IsDisplayNameValid(""));
    }

    [Fact]
    public void DisplayName_RejectsNameLongerThanMaxLength()
    {
        Assert.False(IsDisplayNameValid(new string('A', ModelConstants.ParticipantDisplayNameMaxLength + 1)));
    }

    [Fact]
    public void DisplayName_AcceptsExactlyMaxLength()
    {
        Assert.True(IsDisplayNameValid(new string('A', ModelConstants.ParticipantDisplayNameMaxLength)));
    }

    [Theory]
    [InlineData("Jordan")]
    [InlineData("J")] // minimum length (1)
    [InlineData("山田太郎")] // non-Latin script (Japanese) stays allowed
    [InlineData("O'Brien-Smith")] // ordinary punctuation stays allowed
    [InlineData(" Jordan")] // leading whitespace is allowed by the pattern -- trimmed by the caller before persisting
    [InlineData("Jordan ")] // trailing whitespace likewise allowed here, trimmed by the caller
    public void DisplayName_AcceptsNamesWithNonAsciiOrPunctuation(string displayName)
    {
        Assert.True(IsDisplayNameValid(displayName));
    }

    [Theory]
    [InlineData("   ")] // whitespace-only -- the gap [Required] alone doesn't close
    [InlineData("\t\t")] // whitespace-only (tabs)
    public void DisplayName_RejectsWhitespaceOnlyValue(string displayName)
    {
        Assert.False(IsDisplayNameValid(displayName));
    }

    [Theory]
    [InlineData("Jordan\u0000")] // embedded NUL control character
    [InlineData("Jordan\u0007")] // embedded BEL control character (within C0 range)
    [InlineData("Jordan\u001F")] // embedded C0 control character (upper boundary)
    [InlineData("Jordan\u007F")] // embedded DEL control character
    [InlineData("Jordan\u009F")] // embedded C1 control character (upper boundary)
    [InlineData("Jordan\u200B")] // embedded zero-width space
    [InlineData("Jordan\u200C")] // embedded zero-width non-joiner
    [InlineData("Jordan\u200D")] // embedded zero-width joiner
    [InlineData("Jordan\u200E")] // embedded left-to-right mark
    [InlineData("Jordan\u200F")] // embedded right-to-left mark
    [InlineData("Jordan\uFEFF")] // embedded byte-order mark
    public void DisplayName_RejectsControlAndInvisibleCharacters(string displayName)
    {
        Assert.False(IsDisplayNameValid(displayName));
    }

    [Fact]
    public void Color_RejectsEmptyString()
    {
        // Unlike Participant.Color/ApplicationUser.Color, this one already has [Required] — confirms it
        // actually blocks empty input, as a regression guard for the form users actually submit.
        Assert.False(IsColorValid(""));
    }

    [Theory]
    [InlineData("ff66c")] // too short
    [InlineData("ff66c44")] // too long
    [InlineData("gg66c4")] // non-hex character
    public void Color_RejectsInvalidHexColors(string color)
    {
        Assert.False(IsColorValid(color));
    }
}
