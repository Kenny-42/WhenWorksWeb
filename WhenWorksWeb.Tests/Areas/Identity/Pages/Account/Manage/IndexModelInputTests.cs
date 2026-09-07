using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Areas.Identity.Pages.Account.Manage;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Tier 1 unit tests for <see cref="IndexModel.InputModel.DisplayName"/>'s validation attributes,
/// added by Spec/Features/FEATURES-tighten-account-validation.ospec (Issue #81). Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual production validation attributes,
/// matching the pattern in <c>IndexViewModelTests</c>/<c>ParticipantTests</c>.
/// </summary>
public class IndexModelInputTests
{
    private static bool IsDisplayNameValid(string? candidate)
    {
        var model = new IndexModel.InputModel { Color = "ff66c4" };
        var context = new ValidationContext(model) { MemberName = nameof(IndexModel.InputModel.DisplayName) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
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
