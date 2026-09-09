using System.ComponentModel.DataAnnotations;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 1 unit tests for <see cref="EventAddFinalDateViewModel"/>'s validation attributes. Date
/// format/range/count checks live in the controller (see <c>EventsControllerFinalDateTests</c>) —
/// this only covers the one DataAnnotations attribute the type itself carries.
/// </summary>
public class EventAddFinalDateViewModelTests
{
    private static bool IsStartDateValid(string? candidate)
    {
        var model = new EventAddFinalDateViewModel();
        var context = new ValidationContext(model) { MemberName = nameof(EventAddFinalDateViewModel.StartDate) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Fact]
    public void StartDate_AcceptsNonEmptyValue()
    {
        Assert.True(IsStartDateValid("2026-08-28"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void StartDate_RejectsMissingOrEmptyValue(string? startDate)
    {
        Assert.False(IsStartDateValid(startDate));
    }
}
