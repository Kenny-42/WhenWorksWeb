using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="Participant.Color"/>'s validation attributes. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual <c>[StringLength]</c>/
/// <c>[RegularExpression]</c> attributes exactly as ASP.NET Core model binding would, rather than
/// reimplementing their match semantics by hand — this is the real production validation path.
/// </summary>
public class ParticipantTests
{
    private static bool IsColorValid(string candidate)
    {
        var participant = new Participant { EventId = 1, DisplayName = "placeholder", Color = ModelConstants.DefaultParticipantColor };
        var context = new ValidationContext(participant) { MemberName = nameof(Participant.Color) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData("ff66c4")]
    [InlineData("FF66C4")]
    [InlineData("000000")]
    [InlineData("ffffff")]
    [InlineData("Ff66C4")]
    public void Color_AcceptsValidHexColors(string color)
    {
        Assert.True(IsColorValid(color));
    }

    [Theory]
    [InlineData("ff66c")] // too short
    [InlineData("ff66c44")] // too long
    [InlineData("gg66c4")] // non-hex character
    [InlineData("#ff66c4")] // includes the leading '#', which callers must strip first
    [InlineData(" ff66c4")] // leading whitespace
    [InlineData("ff66c4 ")] // trailing whitespace
    [InlineData("ff66c4\n")] // one character too long once the trailing newline is counted
    [InlineData("ff\n66c4")] // embedded newline, correct total length but wrong characters
    public void Color_RejectsInvalidHexColors(string color)
    {
        Assert.False(IsColorValid(color));
    }

    [Fact]
    public void Color_RejectsEmptyString()
    {
        // Participant.Color has no [Required] attribute of its own — [RegularExpression] treats null/empty
        // as trivially valid by design (that's [Required]'s job), so the only thing that used to catch an
        // empty color here was [StringLength]'s MinimumLength, which was previously unset (defaulting to 0
        // and therefore accepting ""). See CODING_CONVENTIONS.md's Domain Conventions & Gotchas.
        Assert.False(IsColorValid(""));
    }

    [Fact]
    public void Color_AcceptsDefaultParticipantColor()
    {
        // The app-wide fallback color must itself satisfy the same validation every user-supplied color does.
        Assert.True(IsColorValid(ModelConstants.DefaultParticipantColor));
    }
}
