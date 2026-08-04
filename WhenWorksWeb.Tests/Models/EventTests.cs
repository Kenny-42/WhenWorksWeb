using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="Event.Code"/> and <see cref="Event.Title"/>'s validation attributes. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual <c>[StringLength]</c>/
/// <c>[RegularExpression]</c> attributes exactly as ASP.NET Core model binding would.
/// </summary>
public class EventTests
{
    private static Event CreateValidEvent() => Event.Create("BCDFGH", "placeholder");

    private static bool IsPropertyValid<T>(string propertyName, T candidate)
    {
        var context = new ValidationContext(CreateValidEvent()) { MemberName = propertyName };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsCodeValid(string candidate) => IsPropertyValid(nameof(Event.Code), candidate);

    private static bool IsTitleValid(string candidate) => IsPropertyValid(nameof(Event.Title), candidate);

    [Theory]
    [InlineData("BCDFGH")]
    [InlineData("bcdfgh")]
    [InlineData("234567")]
    public void Code_AcceptsValidCodes(string code)
    {
        Assert.True(IsCodeValid(code));
    }

    [Theory]
    [InlineData("")] // empty — caught by StringLength's exact-length requirement
    [InlineData("AEILOU")] // excluded ambiguous letters
    [InlineData("BCDFG")] // too short
    [InlineData("BCDFGHJ")] // too long
    public void Code_RejectsInvalidCodes(string code)
    {
        // Note: unlike Participant.Color/ApplicationUser.DisplayName, a null Code isn't tested here — Code has
        // no [Required] attribute, but it's also never form-bound (only ever set via Event.Create with an
        // already-generated, already-valid code), and required on the C# property blocks null at compile
        // time. There's no reachable path for a null Code, so there's nothing to fix or usefully assert here.
        Assert.False(IsCodeValid(code));
    }

    [Theory]
    [InlineData("A")] // 1 character (MinimumLength boundary)
    public void Title_AcceptsNameWithinLengthBounds(string title)
    {
        Assert.True(IsTitleValid(title));
    }

    [Fact]
    public void Title_AcceptsExactlyMaxLength()
    {
        var thirtyChars = new string('A', ModelConstants.EventTitleMaxLength);

        Assert.True(IsTitleValid(thirtyChars));
    }

    [Fact]
    public void Title_RejectsEmptyString()
    {
        Assert.False(IsTitleValid(""));
    }

    [Fact]
    public void Title_RejectsNameLongerThanMaxLength()
    {
        var thirtyOneChars = new string('A', ModelConstants.EventTitleMaxLength + 1);

        Assert.False(IsTitleValid(thirtyOneChars));
    }
}
