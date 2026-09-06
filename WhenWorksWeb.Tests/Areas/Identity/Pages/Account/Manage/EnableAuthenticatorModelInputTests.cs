using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Areas.Identity.Pages.Account.Manage;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Tier 1 unit tests for <see cref="EnableAuthenticatorModel.InputModel.Code"/>'s validation
/// attributes (see Spec/Features/FEATURES-two-factor-authentication.ospec), using
/// <see cref="Validator.TryValidateProperty"/> to run the actual production validation attributes
/// rather than re-testing <see cref="WhenWorksWeb.Common.ModelConstants.TwoFactorCodePattern"/> in
/// isolation.
/// </summary>
public class EnableAuthenticatorModelInputTests
{
    private static bool IsCodeValid(string? candidate)
    {
        var model = new EnableAuthenticatorModel.InputModel();
        var context = new ValidationContext(model) { MemberName = nameof(EnableAuthenticatorModel.InputModel.Code) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("123456")]
    [InlineData("999999")]
    public void Code_AcceptsSixDigitCodes(string code)
    {
        Assert.True(IsCodeValid(code));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("")] // empty
    [InlineData("12345")] // one digit short
    [InlineData("1234567")] // one digit too many
    [InlineData("12345a")] // contains a non-digit
    [InlineData("123 456")] // contains whitespace
    public void Code_RejectsNonCompliantValues(string? code)
    {
        Assert.False(IsCodeValid(code));
    }
}
