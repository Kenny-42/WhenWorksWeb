using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Areas.Identity.Pages.Account.Manage;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Tier 1 unit tests for <see cref="IndexModel.InputModel.PhoneNumber"/> and
/// <see cref="IndexModel.InputModel.DisplayName"/>'s validation attributes, added by
/// Spec/Features/FEATURES-tighten-account-validation.ospec (Issue #81). Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual production validation attributes,
/// matching the pattern in <c>IndexViewModelTests</c>/<c>ParticipantTests</c>.
/// </summary>
public class IndexModelInputTests
{
    private static bool IsPhoneNumberValid(string? candidate)
    {
        var model = new IndexModel.InputModel { DisplayName = "placeholder", Color = "ff66c4" };
        var context = new ValidationContext(model) { MemberName = nameof(IndexModel.InputModel.PhoneNumber) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsDisplayNameValid(string? candidate)
    {
        var model = new IndexModel.InputModel { Color = "ff66c4" };
        var context = new ValidationContext(model) { MemberName = nameof(IndexModel.InputModel.DisplayName) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData(null)] // optional field -- missing is valid
    [InlineData("")] // optional field -- empty is valid
    [InlineData("5551234567")] // 10 digits, no '+'
    [InlineData("+15551234567")] // '+' plus country code
    [InlineData("1234567")] // shortest allowed (7 digits)
    [InlineData("123456789012345")] // longest allowed (15 digits)
    public void PhoneNumber_AcceptsValidNumbers(string? phoneNumber)
    {
        Assert.True(IsPhoneNumberValid(phoneNumber));
    }

    [Theory]
    [InlineData("123456")] // one digit short of the 7-digit minimum
    [InlineData("1234567890123456")] // one digit past the 15-digit maximum
    [InlineData("555-123-4567")] // dashes not allowed
    [InlineData("(555) 123-4567")] // parentheses/spaces not allowed
    [InlineData("+1 555 123 4567")] // embedded spaces not allowed
    [InlineData("++15551234567")] // more than one leading '+'
    [InlineData("555123abcd")] // letters not allowed
    [InlineData("5551234-567")] // embedded punctuation
    public void PhoneNumber_RejectsInvalidNumbers(string phoneNumber)
    {
        Assert.False(IsPhoneNumberValid(phoneNumber));
    }

    [Theory]
    [InlineData("Jordan")]
    [InlineData("J")] // minimum length (1)
    [InlineData("\u5C71\u7530\u592A\u90CE")] // non-Latin script (Japanese) stays allowed
    [InlineData("O'Brien-Smith")] // ordinary punctuation stays allowed
    [InlineData(" Jordan")] // leading whitespace is allowed by the pattern -- trimmed by the caller before persisting
    [InlineData("Jordan ")] // trailing whitespace likewise allowed here, trimmed by the caller
    public void DisplayName_AcceptsValidNames(string displayName)
    {
        Assert.True(IsDisplayNameValid(displayName));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("")] // empty
    [InlineData("   ")] // whitespace-only -- the gap [Required] alone doesn't close
    [InlineData("\t\t")] // whitespace-only (tabs)
    [InlineData("ThisNameIsWayTooLongToBeValid")] // exceeds ApplicationUserDisplayNameMaxLength (16)
    public void DisplayName_RejectsInvalidNames(string? displayName)
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
}
