using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Areas.Identity.Pages.Account.Manage;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Tier 1 unit tests for <see cref="ChangePasswordModel.InputModel.NewPassword"/>'s validation
/// attributes, strengthened by Spec/Features/FEATURES-tighten-account-validation.ospec (Issue #81)
/// to require length 8+ plus all four character classes, matching
/// <see cref="WhenWorksWeb.Models.IdentityConfiguration"/>'s server-side password policy. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual production validation attributes.
/// </summary>
public class ChangePasswordModelInputTests
{
    private static bool IsNewPasswordValid(string? candidate)
    {
        var model = new ChangePasswordModel.InputModel { OldPassword = "irrelevant" };
        var context = new ValidationContext(model) { MemberName = nameof(ChangePasswordModel.InputModel.NewPassword) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData("Abcdefg1!")] // exactly meets all four classes, 9 characters
    [InlineData("P@ssw0rd")] // exactly at the 8-character minimum
    [InlineData("C0mpl3x!ty-Check_2024")] // longer, still valid
    public void NewPassword_AcceptsCompliantPasswords(string password)
    {
        Assert.True(IsNewPasswordValid(password));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("")] // empty
    [InlineData("Abcd1!g")] // one character short of the 8-character minimum
    [InlineData("abcdefg1!")] // no uppercase letter
    [InlineData("ABCDEFG1!")] // no lowercase letter
    [InlineData("Abcdefgh!")] // no digit
    [InlineData("Abcdefg12")] // no symbol
    [InlineData("aaaaaaaa")] // fails every character-class requirement at once
    public void NewPassword_RejectsNonCompliantPasswords(string? password)
    {
        Assert.False(IsNewPasswordValid(password));
    }
}
