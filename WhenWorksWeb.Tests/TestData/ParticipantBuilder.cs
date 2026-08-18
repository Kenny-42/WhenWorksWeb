using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.TestData;

/// <summary>
/// Hand-rolled test data builder for <see cref="Participant"/>. Defaults are valid per the real schema
/// constraints (trimmed display name satisfying the DB check constraint, valid hex color) so a test only
/// needs to override the specific field it cares about.
/// </summary>
public sealed class ParticipantBuilder
{
    private int _eventId;
    private string _displayName = "Test Participant";
    private string _color = ModelConstants.DefaultParticipantColor;
    private string? _userId;

    public ParticipantBuilder ForEvent(int eventId)
    {
        _eventId = eventId;
        return this;
    }

    /// <summary>Reads <paramref name="event"/>.Id, so call this after the event has been saved.</summary>
    public ParticipantBuilder ForEvent(Event @event)
    {
        _eventId = @event.Id;
        return this;
    }

    public ParticipantBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    public ParticipantBuilder WithColor(string color)
    {
        _color = color;
        return this;
    }

    public ParticipantBuilder WithUserId(string? userId)
    {
        _userId = userId;
        return this;
    }

    public Participant Build() => new()
    {
        EventId = _eventId,
        DisplayName = _displayName,
        Color = _color,
        UserId = _userId
    };
}
