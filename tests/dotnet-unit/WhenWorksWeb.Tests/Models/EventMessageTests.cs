using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="EventMessage.SenderColor"/>'s validation attributes. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual <c>[StringLength]</c>/
/// <c>[RegularExpression]</c> attributes exactly as ASP.NET Core model binding would.
/// </summary>
public class EventMessageTests
{
    private static bool IsSenderColorValid(string candidate)
    {
        var message = new EventMessage
        {
            EventId = 1,
            SenderDisplayName = "placeholder",
            SenderColor = ModelConstants.DefaultParticipantColor,
            Body = "placeholder",
            SentAt = DateTime.UtcNow
        };
        var context = new ValidationContext(message) { MemberName = nameof(EventMessage.SenderColor) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData("ff66c4")]
    [InlineData("FF66C4")]
    public void SenderColor_AcceptsValidHexColors(string color)
    {
        Assert.True(IsSenderColorValid(color));
    }

    [Fact]
    public void SenderColor_RejectsEmptyString()
    {
        // Same StringLength(MaxLength)-with-no-MinimumLength gap as Participant.Color — see
        // CODING_CONVENTIONS.md's Domain Conventions & Gotchas and ParticipantTests.Color_RejectsEmptyString.
        Assert.False(IsSenderColorValid(""));
    }

    [Theory]
    [InlineData("ff66c")] // too short
    [InlineData("ff66c44")] // too long
    [InlineData("gg66c4")] // non-hex character
    public void SenderColor_RejectsInvalidHexColors(string color)
    {
        Assert.False(IsSenderColorValid(color));
    }
}
