using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Areas.Identity.Pages.Account;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 1 unit tests for <see cref="LoginWith2faModel.InputModel.TwoFactorCode"/>'s validation
/// attributes (see Spec/Features/FEATURES-two-factor-authentication.ospec), using
/// <see cref="Validator.TryValidateProperty"/> to run the actual production validation attributes.
/// </summary>
public class LoginWith2faModelInputTests
{
    private static bool IsTwoFactorCodeValid(string? candidate)
    {
        var model = new LoginWith2faModel.InputModel();
        var context = new ValidationContext(model) { MemberName = nameof(LoginWith2faModel.InputModel.TwoFactorCode) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData("000000")]
    [InlineData("123456")]
    [InlineData("999999")]
    public void TwoFactorCode_AcceptsSixDigitCodes(string code)
    {
        Assert.True(IsTwoFactorCodeValid(code));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("")] // empty
    [InlineData("12345")] // one digit short
    [InlineData("1234567")] // one digit too many
    [InlineData("12345a")] // contains a non-digit
    public void TwoFactorCode_RejectsNonCompliantValues(string? code)
    {
        Assert.False(IsTwoFactorCodeValid(code));
    }
}

/// <summary>
/// Tier 1 unit tests for <see cref="LoginWithRecoveryCodeModel.InputModel.RecoveryCode"/>'s
/// validation attributes.
/// </summary>
public class LoginWithRecoveryCodeModelInputTests
{
    private static bool IsRecoveryCodeValid(string? candidate)
    {
        var model = new LoginWithRecoveryCodeModel.InputModel();
        var context = new ValidationContext(model) { MemberName = nameof(LoginWithRecoveryCodeModel.InputModel.RecoveryCode) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData("abcde-12345")] // typical Identity-generated recovery code shape
    [InlineData("x")] // any non-empty value is accepted here -- real validity is checked by UserManager
    public void RecoveryCode_AcceptsNonEmptyValues(string code)
    {
        Assert.True(IsRecoveryCodeValid(code));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("")] // empty
    public void RecoveryCode_RejectsMissingValues(string? code)
    {
        Assert.False(IsRecoveryCodeValid(code));
    }

    [Fact]
    public void RecoveryCode_RejectsValueLongerThanMaxLength()
    {
        var tooLong = new string('a', WhenWorksWeb.Common.ModelConstants.RecoveryCodeMaxLength + 1);
        Assert.False(IsRecoveryCodeValid(tooLong));
    }
}
