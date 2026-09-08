using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="ApplicationUser"/>'s validation attributes. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual <c>[StringLength]</c>/
/// <c>[RegularExpression]</c> attributes exactly as ASP.NET Core model binding would.
/// </summary>
public class ApplicationUserTests
{
    private static bool IsPropertyValid<T>(string propertyName, T candidate)
    {
        var user = new ApplicationUser { CreatedAt = DateTime.UtcNow, LastActiveAt = DateTime.UtcNow };
        var context = new ValidationContext(user) { MemberName = propertyName };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsColorValid(string candidate) => IsPropertyValid(nameof(ApplicationUser.Color), candidate);

    private static bool IsDisplayNameValid(string candidate) => IsPropertyValid(nameof(ApplicationUser.DisplayName), candidate);

    [Theory]
    [InlineData("ff66c4")]
    [InlineData("FF66C4")]
    public void Color_AcceptsValidHexColors(string color)
    {
        Assert.True(IsColorValid(color));
    }

    [Fact]
    public void Color_RejectsEmptyString()
    {
        // Same StringLength(MaxLength)-with-no-MinimumLength gap as Participant.Color — see
        // CODING_CONVENTIONS.md's Domain Conventions & Gotchas and ParticipantTests.Color_RejectsEmptyString.
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

    [Fact]
    public void Color_DefaultValueIsValid()
    {
        var user = new ApplicationUser { CreatedAt = DateTime.UtcNow, LastActiveAt = DateTime.UtcNow };

        Assert.True(IsColorValid(user.Color));
    }

    [Theory]
    [InlineData("A")] // 1 character
    [InlineData("Sixteen Chars!!!")] // exactly 16 characters
    public void DisplayName_AcceptsNameWithinMaxLength(string displayName)
    {
        Assert.True(IsDisplayNameValid(displayName));
    }

    [Fact]
    public void DisplayName_RejectsNameLongerThanMaxLength()
    {
        var seventeenChars = new string('A', ModelConstants.ApplicationUserDisplayNameMaxLength + 1);

        Assert.False(IsDisplayNameValid(seventeenChars));
    }

    [Fact]
    public void DisplayName_RejectsEmptyString()
    {
        // Same StringLength(MaxLength)-with-no-MinimumLength gap as Color had (see
        // ParticipantTests.Color_RejectsEmptyString) — found and fixed the same way here.
        Assert.False(IsDisplayNameValid(""));
    }

    [Fact]
    public void DisplayName_DefaultValueIsValid()
    {
        var user = new ApplicationUser { CreatedAt = DateTime.UtcNow, LastActiveAt = DateTime.UtcNow };

        Assert.True(IsDisplayNameValid(user.DisplayName));
    }

    // Added by Spec/Bugs/BUGS-missing-download-personal-data-page.ospec (Issue #107):
    // DownloadPersonalDataModel exports whichever ApplicationUser properties carry
    // [PersonalData], reflecting over the attribute rather than a hardcoded field list -- these
    // guard the actual export contract against a property losing its attribute unnoticed.
    [Theory]
    [InlineData(nameof(ApplicationUser.DisplayName))]
    [InlineData(nameof(ApplicationUser.Color))]
    [InlineData(nameof(ApplicationUser.CreatedAt))]
    [InlineData(nameof(ApplicationUser.LastActiveAt))]
    public void PersonalDataProperty_IsAttributedForExport(string propertyName)
    {
        var property = typeof(ApplicationUser).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!;

        Assert.True(Attribute.IsDefined(property, typeof(PersonalDataAttribute)));
    }
}
