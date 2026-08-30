using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="IndexViewModel.EventCode"/> and
/// <see cref="IndexViewModel.CreateEventName"/>'s validation attributes. Uses
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

    private static bool IsCreateEventNameValid(string? candidate)
    {
        var model = new IndexViewModel();
        var context = new ValidationContext(model) { MemberName = nameof(IndexViewModel.CreateEventName) };
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

    [Theory]
    [InlineData("Trivia Night")]
    [InlineData("A")] // 1 character (MinimumLength boundary)
    [InlineData("山田太郎")] // non-Latin script (Japanese) stays allowed
    [InlineData("O'Brien's Party!")] // ordinary punctuation stays allowed
    [InlineData(" Trivia Night")] // leading whitespace is allowed by the pattern -- trimmed by the caller before persisting
    [InlineData("Trivia Night ")] // trailing whitespace likewise allowed here, trimmed by the caller
    public void CreateEventName_AcceptsValidNames(string createEventName)
    {
        Assert.True(IsCreateEventNameValid(createEventName));
    }

    [Fact]
    public void CreateEventName_AcceptsExactlyMaxLength()
    {
        var thirtyChars = new string('A', ModelConstants.EventTitleMaxLength);

        Assert.True(IsCreateEventNameValid(thirtyChars));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("")] // empty
    [InlineData("   ")] // whitespace-only -- the gap [Required] alone doesn't close
    [InlineData("\t\t")] // whitespace-only (tabs)
    public void CreateEventName_RejectsMissingOrEmptyValue(string? createEventName)
    {
        Assert.False(IsCreateEventNameValid(createEventName));
    }

    [Fact]
    public void CreateEventName_RejectsNameLongerThanMaxLength()
    {
        var thirtyOneChars = new string('A', ModelConstants.EventTitleMaxLength + 1);

        Assert.False(IsCreateEventNameValid(thirtyOneChars));
    }

    [Theory]
    [InlineData("Trivia\u0000Night")] // embedded NUL control character
    [InlineData("Trivia\u0007Night")] // embedded BEL control character (within C0 range)
    [InlineData("Trivia\u001FNight")] // embedded C0 control character (upper boundary)
    [InlineData("Trivia\u007FNight")] // embedded DEL control character
    [InlineData("Trivia\u009FNight")] // embedded C1 control character (upper boundary)
    [InlineData("Trivia\u200BNight")] // embedded zero-width space
    [InlineData("Trivia\u200CNight")] // embedded zero-width non-joiner
    [InlineData("Trivia\u200DNight")] // embedded zero-width joiner
    [InlineData("Trivia\u200ENight")] // embedded left-to-right mark
    [InlineData("Trivia\u200FNight")] // embedded right-to-left mark
    [InlineData("Trivia\uFEFFNight")] // embedded byte-order mark
    public void CreateEventName_RejectsControlAndInvisibleCharacters(string createEventName)
    {
        Assert.False(IsCreateEventNameValid(createEventName));
    }
}
