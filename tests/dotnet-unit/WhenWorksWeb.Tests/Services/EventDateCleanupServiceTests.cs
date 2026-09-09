using WhenWorksWeb.Models;
using WhenWorksWeb.Services;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Services;

/// <summary>
/// Unit tests for <see cref="EventDateCleanupService"/> — the shared helper that deletes
/// candidate dates left with zero availability marks, used by both
/// <c>EventsController.Availability.cs</c> and <c>MyEventsController</c>'s participant deletion.
/// </summary>
public class EventDateCleanupServiceTests : SqliteDbContextFixture
{
    private async Task<Event> CreateEventAsync(string code = "BCDFGH")
    {
        var evt = new EventBuilder().WithCode(code).Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();
        return evt;
    }

    [Fact]
    public async Task RemoveEmptyDatesAsync_WithNoEventDates_ReturnsZero()
    {
        var evt = await CreateEventAsync();
        var service = new EventDateCleanupService(Db);

        var removed = await service.RemoveEmptyDatesAsync(evt.Id, CancellationToken.None);

        Assert.Equal(0, removed);
    }

    [Fact]
    public async Task RemoveEmptyDatesAsync_DeletesDatesWithZeroAvailabilities()
    {
        var evt = await CreateEventAsync();
        var eventDate = new EventDateBuilder().ForEvent(evt).Build();
        Db.EventDates.Add(eventDate);
        await Db.SaveChangesAsync();

        var service = new EventDateCleanupService(Db);
        var removed = await service.RemoveEmptyDatesAsync(evt.Id, CancellationToken.None);

        Assert.Equal(1, removed);
        Assert.Empty(Db.EventDates);
    }

    [Fact]
    public async Task RemoveEmptyDatesAsync_LeavesDatesWithAtLeastOneAvailabilityUntouched()
    {
        var evt = await CreateEventAsync();
        var participant = new ParticipantBuilder().ForEvent(evt).Build();
        Db.Participants.Add(participant);
        await Db.SaveChangesAsync();

        var eventDate = new EventDateBuilder().ForEvent(evt).Build();
        Db.EventDates.Add(eventDate);
        await Db.SaveChangesAsync();

        Db.ParticipantAvailabilities.Add(new ParticipantAvailability { ParticipantId = participant.Id, EventDateId = eventDate.Id });
        await Db.SaveChangesAsync();

        var service = new EventDateCleanupService(Db);
        var removed = await service.RemoveEmptyDatesAsync(evt.Id, CancellationToken.None);

        Assert.Equal(0, removed);
        Assert.Single(Db.EventDates);
    }

    [Fact]
    public async Task RemoveEmptyDatesAsync_WithMixOfEmptyAndOccupiedDates_RemovesOnlyTheEmptyOnes()
    {
        var evt = await CreateEventAsync();
        var participant = new ParticipantBuilder().ForEvent(evt).Build();
        Db.Participants.Add(participant);
        await Db.SaveChangesAsync();

        var occupiedDate = new EventDateBuilder().ForEvent(evt).WithDate(new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero)).Build();
        var emptyDate = new EventDateBuilder().ForEvent(evt).WithDate(new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero)).Build();
        Db.EventDates.AddRange(occupiedDate, emptyDate);
        await Db.SaveChangesAsync();

        Db.ParticipantAvailabilities.Add(new ParticipantAvailability { ParticipantId = participant.Id, EventDateId = occupiedDate.Id });
        await Db.SaveChangesAsync();

        var service = new EventDateCleanupService(Db);
        var removed = await service.RemoveEmptyDatesAsync(evt.Id, CancellationToken.None);

        Assert.Equal(1, removed);
        var survivor = Assert.Single(Db.EventDates);
        Assert.Equal(occupiedDate.Id, survivor.Id);
    }

    [Fact]
    public async Task RemoveEmptyDatesAsync_OnlyAffectsTheGivenEvent()
    {
        var eventA = await CreateEventAsync("BCDFGH");
        var eventB = await CreateEventAsync("BCDFGJ");

        var emptyDateInA = new EventDateBuilder().ForEvent(eventA).Build();
        var emptyDateInB = new EventDateBuilder().ForEvent(eventB).Build();
        Db.EventDates.AddRange(emptyDateInA, emptyDateInB);
        await Db.SaveChangesAsync();

        var service = new EventDateCleanupService(Db);
        var removed = await service.RemoveEmptyDatesAsync(eventA.Id, CancellationToken.None);

        Assert.Equal(1, removed);
        var survivor = Assert.Single(Db.EventDates);
        Assert.Equal(eventB.Id, survivor.EventId);
    }
}
