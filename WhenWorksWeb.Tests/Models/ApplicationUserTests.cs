using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="ApplicationUser.Color"/>'s validation attributes. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual <c>[StringLength]</c>/
/// <c>[RegularExpression]</c> attributes exactly as ASP.NET Core model binding would.
/// </summary>
public class ApplicationUserTests
{
    private static bool IsColorValid(string candidate)
    {
        var user = new ApplicationUser
        {
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };
        var context = new ValidationContext(user) { MemberName = nameof(ApplicationUser.Color) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

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
}
