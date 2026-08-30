using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Areas.Identity.Pages.Account;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 1 unit tests for <see cref="ExternalLoginModel.InputModel.DisplayName"/>'s validation
/// attributes, brought in line with Register/Manage/Index's rule by
/// Spec/Features/FEATURES-tighten-account-validation.ospec (Issue #81) -- this class's own XML doc
/// comment says it matches "the same rule Register.cshtml.cs uses," so it must actually carry the
/// same content-pattern check, not just the length check. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual production validation attributes.
/// </summary>
public class ExternalLoginModelInputTests
{
    private static bool IsDisplayNameValid(string? candidate)
    {
        var model = new ExternalLoginModel.InputModel { UserName = "placeholder", Email = "placeholder@example.com", Color = "ff66c4" };
        var context = new ValidationContext(model) { MemberName = nameof(ExternalLoginModel.InputModel.DisplayName) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
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
}
