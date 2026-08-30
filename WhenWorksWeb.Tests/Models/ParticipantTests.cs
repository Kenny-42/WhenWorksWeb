using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="Participant.Color"/> and <see cref="Participant.DisplayName"/>'s
/// validation attributes. Uses <see cref="Validator.TryValidateProperty"/> to run the actual
/// <c>[StringLength]</c>/<c>[RegularExpression]</c> attributes exactly as ASP.NET Core model binding would,
/// rather than reimplementing their match semantics by hand — this is the real production validation path.
/// </summary>
public class ParticipantTests
{
    private static Participant CreateValidParticipant() =>
        new() { EventId = 1, DisplayName = "placeholder", Color = ModelConstants.DefaultParticipantColor };

    private static bool IsPropertyValid<T>(string propertyName, T candidate)
    {
        var context = new ValidationContext(CreateValidParticipant()) { MemberName = propertyName };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsColorValid(string candidate) => IsPropertyValid(nameof(Participant.Color), candidate);

    private static bool IsDisplayNameValid(string candidate) => IsPropertyValid(nameof(Participant.DisplayName), candidate);

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

    [Theory]
    [InlineData("A")] // 1 character (MinimumLength boundary)
    [InlineData("Sixteen Chars!!!")] // exactly 16 characters (MaxLength boundary)
    public void DisplayName_AcceptsNameWithinLengthBounds(string displayName)
    {
        Assert.True(IsDisplayNameValid(displayName));
    }

    [Fact]
    public void DisplayName_RejectsEmptyString()
    {
        // Unlike Color, DisplayName already has MinimumLength = 1 set explicitly — this confirms it actually
        // works, as a regression guard rather than a newly-found gap.
        Assert.False(IsDisplayNameValid(""));
    }

    [Fact]
    public void DisplayName_RejectsNameLongerThanMaxLength()
    {
        var seventeenChars = new string('A', ModelConstants.ParticipantDisplayNameMaxLength + 1);

        Assert.False(IsDisplayNameValid(seventeenChars));
    }

    [Theory]
    [InlineData("Jordan")]
    [InlineData("山田太郎")] // non-Latin script (Japanese) stays allowed
    [InlineData("O'Brien-Smith")] // ordinary punctuation stays allowed
    [InlineData(" Jordan")] // leading whitespace is allowed by the pattern -- trimmed by the caller before persisting
    [InlineData("Jordan ")] // trailing whitespace likewise allowed here, trimmed by the caller
    public void DisplayName_AcceptsNamesWithNonAsciiOrPunctuation(string displayName)
    {
        Assert.True(IsDisplayNameValid(displayName));
    }

    [Theory]
    [InlineData("   ")] // whitespace-only -- the gap [Required]/[StringLength] alone doesn't close
    [InlineData("\t\t")] // whitespace-only (tabs)
    public void DisplayName_RejectsWhitespaceOnlyValue(string displayName)
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
