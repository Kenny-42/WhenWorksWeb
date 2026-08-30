using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="EventUpdateDetailsViewModel"/>'s validation attributes. Uses
/// <see cref="Validator.TryValidateProperty"/> to run the actual attributes exactly as ASP.NET
/// Core model binding would, rather than reimplementing their match semantics by hand — see
/// CODING_CONVENTIONS.md's Testing Conventions section for why.
/// </summary>
public class EventUpdateDetailsViewModelTests
{
    private static bool IsTitleValid(string? candidate)
    {
        var model = new EventUpdateDetailsViewModel();
        var context = new ValidationContext(model) { MemberName = nameof(EventUpdateDetailsViewModel.Title) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsDescriptionValid(string? candidate)
    {
        var model = new EventUpdateDetailsViewModel { Title = "placeholder" };
        var context = new ValidationContext(model) { MemberName = nameof(EventUpdateDetailsViewModel.Description) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    private static bool IsEmojiValid(string? candidate)
    {
        var model = new EventUpdateDetailsViewModel { Title = "placeholder" };
        var context = new ValidationContext(model) { MemberName = nameof(EventUpdateDetailsViewModel.Emoji) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Trivia Night")]
    public void Title_AcceptsNonEmptyValueWithinMaxLength(string title)
    {
        Assert.True(IsTitleValid(title));
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("")] // empty
    public void Title_RejectsMissingOrEmptyValue(string? title)
    {
        Assert.False(IsTitleValid(title));
    }

    [Fact]
    public void Title_RejectsValueOverMaxLength()
    {
        var tooLong = new string('a', ModelConstants.EventTitleMaxLength + 1);
        Assert.False(IsTitleValid(tooLong));
    }

    [Fact]
    public void Title_AcceptsValueAtMaxLength()
    {
        var atMax = new string('a', ModelConstants.EventTitleMaxLength);
        Assert.True(IsTitleValid(atMax));
    }

    [Fact]
    public void Description_AcceptsNullOrEmpty()
    {
        Assert.True(IsDescriptionValid(null));
        Assert.True(IsDescriptionValid(""));
    }

    [Fact]
    public void Description_RejectsValueOverMaxLength()
    {
        var tooLong = new string('a', ModelConstants.EventDescriptionMaxLength + 1);
        Assert.False(IsDescriptionValid(tooLong));
    }

    [Fact]
    public void Description_AcceptsValueAtMaxLength()
    {
        var atMax = new string('a', ModelConstants.EventDescriptionMaxLength);
        Assert.True(IsDescriptionValid(atMax));
    }

    [Fact]
    public void Emoji_AcceptsNullOrEmpty()
    {
        Assert.True(IsEmojiValid(null));
        Assert.True(IsEmojiValid(""));
    }

    [Fact]
    public void Emoji_AcceptsSingleCodepointEmoji()
    {
        Assert.True(IsEmojiValid("🎲"));
    }

    [Theory]
    [InlineData("🎉🎉")]
    [InlineData("hi")]
    public void Emoji_RejectsMultiCharacterValue(string invalid)
    {
        Assert.False(IsEmojiValid(invalid));
    }
}
