using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Areas.Identity.Pages.Account;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 1 unit tests for <see cref="ResetPasswordModel.InputModel.Password"/>'s validation
/// attributes, brought in line with the Manage pages' rules by
/// Spec/Features/FEATURES-tighten-account-validation.ospec (Issue #81) -- previously a bare
/// 6-100 character length check, same gap the issue reported on ChangePassword/SetPassword. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual production validation attributes.
/// </summary>
public class ResetPasswordModelInputTests
{
    private static bool IsPasswordValid(string? candidate)
    {
        var model = new ResetPasswordModel.InputModel { Email = "placeholder@example.com", Code = "placeholder" };
        var context = new ValidationContext(model) { MemberName = nameof(ResetPasswordModel.InputModel.Password) };
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
    [InlineData("Abcd1!g")] // one character short of the 8-character minimum
    [InlineData("abcdefg1!")] // no uppercase letter
    [InlineData("ABCDEFG1!")] // no lowercase letter
    [InlineData("Abcdefgh!")] // no digit
    [InlineData("Abcdefg12")] // no symbol
    public void Password_RejectsNonCompliantPasswords(string? password)
    {
        Assert.False(IsPasswordValid(password));
    }
}
