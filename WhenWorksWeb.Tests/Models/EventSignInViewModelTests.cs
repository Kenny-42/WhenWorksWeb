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
