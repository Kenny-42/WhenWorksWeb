using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Areas.Identity.Pages.Account;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 1 unit tests for <see cref="RegisterModel.InputModel"/>'s Password/DisplayName/Color
/// validation attributes, brought in line with the Manage pages' rules by
/// Spec/Features/FEATURES-tighten-account-validation.ospec (Issue #81) so a value that passes
/// Register doesn't turn around and fail the server-side <c>IdentityConfiguration</c> policy, or
/// end up looser than what Manage/Index.cshtml.cs now accepts for the same fields. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual production validation attributes.
/// </summary>
public class RegisterModelInputTests
{
    private static bool IsPasswordValid(string? candidate)
    {
        var model = new RegisterModel.InputModel { UserName = "placeholder", Email = "placeholder@example.com", DisplayName = "placeholder", Color = "ff66c4" };
        var context = new ValidationContext(model) { MemberName = nameof(RegisterModel.InputModel.Password) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsDisplayNameValid(string? candidate)
    {
        var model = new RegisterModel.InputModel { UserName = "placeholder", Email = "placeholder@example.com", Color = "ff66c4" };
        var context = new ValidationContext(model) { MemberName = nameof(RegisterModel.InputModel.DisplayName) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsColorValid(string? candidate)
    {
        var model = new RegisterModel.InputModel { UserName = "placeholder", Email = "placeholder@example.com", DisplayName = "placeholder" };
        var context = new ValidationContext(model) { MemberName = nameof(RegisterModel.InputModel.Color) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData("Abcdefg1!")]
    [InlineData("P@ssw0rd")]
    public void Password_AcceptsCompliantPasswords(string password)
    {
        Assert.True(IsPasswordValid(password));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("")] // empty
    [InlineData("Dev123")] // the old dev-seed default -- no longer compliant, guards against regressing to it
    [InlineData("abcdefg1!")] // no uppercase letter
    [InlineData("Abcdefgh!")] // no digit
    [InlineData("Abcdefg12")] // no symbol
    public void Password_RejectsNonCompliantPasswords(string? password)
    {
        Assert.False(IsPasswordValid(password));
    }

    [Theory]
    [InlineData("Jordan")]
    [InlineData(" Jordan")] // leading whitespace allowed by the pattern -- trimmed by the caller before persisting
    public void DisplayName_AcceptsValidNames(string displayName)
    {
        Assert.True(IsDisplayNameValid(displayName));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("   ")] // whitespace-only
    [InlineData("Jordan​")] // embedded zero-width space
    public void DisplayName_RejectsInvalidNames(string? displayName)
    {
        Assert.False(IsDisplayNameValid(displayName));
    }

    [Theory]
    [InlineData("ff66c4")]
    [InlineData("FF66C4")]
    public void Color_AcceptsValidHexColors(string color)
    {
        Assert.True(IsColorValid(color));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("ff66c")] // too short
    [InlineData("ff66c44")] // too long
    [InlineData("gg66c4")] // non-hex character
    public void Color_RejectsInvalidHexColors(string? color)
    {
        Assert.False(IsColorValid(color));
    }
}
