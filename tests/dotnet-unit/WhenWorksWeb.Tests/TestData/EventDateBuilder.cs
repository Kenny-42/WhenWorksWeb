using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.TestData;

/// <summary>
/// Hand-rolled test data builder for <see cref="EventDate"/>.
/// </summary>
public sealed class EventDateBuilder
{
    private int _eventId;
    private DateTimeOffset _date = DateTimeOffset.UtcNow.Date.AddDays(1);

    public EventDateBuilder ForEvent(int eventId)
    {
        _eventId = eventId;
        return this;
    }

    /// <summary>Reads <paramref name="event"/>.Id, so call this after the event has been saved.</summary>
    public EventDateBuilder ForEvent(Event @event)
    {
        _eventId = @event.Id;
        return this;
    }

    public EventDateBuilder WithDate(DateTimeOffset date)
    {
        _date = date;
        return this;
    }

    public EventDate Build() => new() { EventId = _eventId, Date = _date };
}
