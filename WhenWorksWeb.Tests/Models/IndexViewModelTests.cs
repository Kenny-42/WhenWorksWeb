using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="IndexViewModel.EventCode"/>'s validation attributes. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual <c>[Required]</c>/<c>[StringLength]</c>/
/// <c>[RegularExpression]</c> attributes exactly as ASP.NET Core model binding would, rather than
/// reimplementing their match semantics by hand — this is the real production validation path.
/// </summary>
public class IndexViewModelTests
{
    private static bool IsEventCodeValid(string? candidate)
    {
        var model = new IndexViewModel { CreateEventName = "placeholder" };
        var context = new ValidationContext(model) { MemberName = nameof(IndexViewModel.EventCode) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData("BCDFGH")]
    [InlineData("bcdfgh")]
    [InlineData("BcDfGh")]
    [InlineData("234567")]
    public void EventCode_AcceptsValidCodes(string code)
    {
        Assert.True(IsEventCodeValid(code));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("")] // empty
    [InlineData("AEILOU")] // excluded ambiguous letters
    [InlineData("012345")] // excluded ambiguous digits (0 and 1)
    [InlineData("BCDFG")] // too short
    [InlineData("BCDFGHJ")] // too long
    [InlineData("BCDFG!")] // non-alphabet character
    [InlineData(" BCDFG")] // leading whitespace
    [InlineData("BCDFG ")] // trailing whitespace
    [InlineData("BCDFGH\n")] // one character too long once the trailing newline is counted
    [InlineData("BC\nDFGH")] // embedded newline, correct total length but wrong characters
    public void EventCode_RejectsInvalidCodes(string? code)
    {
        Assert.False(IsEventCodeValid(code));
    }
}
