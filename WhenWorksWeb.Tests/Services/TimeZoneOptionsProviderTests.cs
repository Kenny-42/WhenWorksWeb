using WhenWorksWeb.Services;

namespace WhenWorksWeb.Tests.Services;

/// <summary>
/// Tests for <see cref="TimeZoneOptionsProvider"/>: the grouped/sorted option list backing the
/// event timezone picker, and the canonical-IANA-id validation gate in front of
/// <c>EventsController.Create</c> and <c>UpdateTimeZone</c>.
/// </summary>
public class TimeZoneOptionsProviderTests
{
    private static TimeZoneOptionsProvider CreateProvider() => new();

    // ---- IsValidTimeZoneId ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidTimeZoneId_WithNullOrWhitespace_ReturnsFalse(string? timeZoneId)
    {
        var provider = CreateProvider();

        Assert.False(provider.IsValidTimeZoneId(timeZoneId));
    }

    [Fact]
    public void IsValidTimeZoneId_WithKnownIanaId_ReturnsTrue()
    {
        var provider = CreateProvider();

        Assert.True(provider.IsValidTimeZoneId("America/New_York"));
    }

    [Fact]
    public void IsValidTimeZoneId_WithUtc_ReturnsTrue()
    {
        var provider = CreateProvider();

        Assert.True(provider.IsValidTimeZoneId("UTC"));
    }

    [Fact]
    public void IsValidTimeZoneId_WithGarbageString_ReturnsFalse()
    {
        var provider = CreateProvider();

        Assert.False(provider.IsValidTimeZoneId("Not/A/Real/Zone"));
    }

    /// <summary>
    /// The regression case behind this fix: <see cref="TimeZoneInfo.TryFindSystemTimeZoneById"/>
    /// resolves Windows-style ids on any platform (including this one, wherever it runs), but
    /// <see cref="Event.TimeZoneId"/> commits to storing IANA ids only — a Windows id must be
    /// rejected here even though the BCL itself would resolve it.
    /// </summary>
    [Fact]
    public void IsValidTimeZoneId_WithWindowsStyleId_ReturnsFalse()
    {
        var provider = CreateProvider();

        Assert.False(provider.IsValidTimeZoneId("Eastern Standard Time"));
    }

    // ---- GetGroupedOptions ----

    [Fact]
    public void GetGroupedOptions_ReturnsNonEmptyGroupsAndOptions()
    {
        var provider = CreateProvider();

        var groups = provider.GetGroupedOptions();

        Assert.NotEmpty(groups);
        Assert.All(groups, g => Assert.NotEmpty(g.Options));
    }

    [Fact]
    public void GetGroupedOptions_IncludesAKnownZoneInItsExpectedGroup()
    {
        var provider = CreateProvider();

        var groups = provider.GetGroupedOptions();

        var americaGroup = Assert.Single(groups, g => g.GroupLabel == "America");
        Assert.Contains(americaGroup.Options, o => o.Id == "America/New_York");
    }

    [Fact]
    public void GetGroupedOptions_PutsIdWithNoSlashInOtherGroup()
    {
        var provider = CreateProvider();

        var groups = provider.GetGroupedOptions();

        var otherGroup = Assert.Single(groups, g => g.GroupLabel == "Other");
        Assert.Contains(otherGroup.Options, o => o.Id == "UTC");
    }

    [Fact]
    public void GetGroupedOptions_GroupsAreSortedAlphabeticallyByLabel()
    {
        var provider = CreateProvider();

        var groups = provider.GetGroupedOptions();

        var labels = groups.Select(g => g.GroupLabel).ToList();
        Assert.Equal(labels.OrderBy(l => l, StringComparer.Ordinal), labels);
    }

    [Fact]
    public void GetGroupedOptions_OptionsWithinAGroupAreSortedByOffsetThenId()
    {
        var provider = CreateProvider();

        var groups = provider.GetGroupedOptions();

        foreach (var group in groups)
        {
            var ids = group.Options.Select(o => o.Id).ToList();
            var expectedOrder = group.Options
                .OrderBy(o => TimeZoneInfo.FindSystemTimeZoneById(o.Id).GetUtcOffset(DateTimeOffset.UtcNow))
                .ThenBy(o => o.Id, StringComparer.Ordinal)
                .Select(o => o.Id)
                .ToList();
            Assert.Equal(expectedOrder, ids);
        }
    }

    [Fact]
    public void GetGroupedOptions_EveryOptionIdIsAlsoValid()
    {
        var provider = CreateProvider();

        var groups = provider.GetGroupedOptions();

        Assert.All(groups.SelectMany(g => g.Options), o => Assert.True(provider.IsValidTimeZoneId(o.Id)));
    }

    [Fact]
    public void GetGroupedOptions_ContainsNoDuplicateIds()
    {
        var provider = CreateProvider();

        var ids = provider.GetGroupedOptions().SelectMany(g => g.Options).Select(o => o.Id).ToList();

        Assert.Equal(ids.Distinct(StringComparer.Ordinal).Count(), ids.Count);
    }

    [Fact]
    public void GetGroupedOptions_FormatsLabelWithUtcOffsetPrefix()
    {
        var provider = CreateProvider();

        var utcOption = provider.GetGroupedOptions()
            .SelectMany(g => g.Options)
            .Single(o => o.Id == "UTC");

        Assert.Equal("(UTC+00:00) UTC", utcOption.Label);
    }

    /// <summary>
    /// Two independently constructed instances must agree on the canonical set — the perf fix
    /// resolves it once per instance at construction rather than lazily/globally, so this guards
    /// against that caching accidentally becoming stateful/order-dependent across instances.
    /// </summary>
    [Fact]
    public void GetGroupedOptions_IsConsistentAcrossSeparateProviderInstances()
    {
        var first = CreateProvider().GetGroupedOptions().SelectMany(g => g.Options).Select(o => o.Id).ToList();
        var second = CreateProvider().GetGroupedOptions().SelectMany(g => g.Options).Select(o => o.Id).ToList();

        Assert.Equal(first, second);
    }
}
