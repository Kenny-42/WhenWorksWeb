using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.TestData;

/// <summary>
/// Hand-rolled test data builder for <see cref="EventMessage"/>.
/// </summary>
public sealed class EventMessageBuilder
{
    private int _eventId;
    private int? _participantId;
    private string _senderDisplayName = "Test Participant";
    private string _senderColor = ModelConstants.DefaultParticipantColor;
    private string _body = "Test message";
    private DateTime _sentAt = DateTime.UtcNow;

    public EventMessageBuilder ForEvent(int eventId)
    {
        _eventId = eventId;
        return this;
    }

    /// <summary>Reads <paramref name="event"/>.Id, so call this after the event has been saved.</summary>
    public EventMessageBuilder ForEvent(Event @event)
    {
        _eventId = @event.Id;
        return this;
    }

    /// <summary>Null represents a message from a since-deleted participant (see EventMessage.ParticipantId).</summary>
    public EventMessageBuilder FromParticipant(int? participantId)
    {
        _participantId = participantId;
        return this;
    }

    public EventMessageBuilder WithSenderDisplayName(string senderDisplayName)
    {
        _senderDisplayName = senderDisplayName;
        return this;
    }

    public EventMessageBuilder WithSenderColor(string senderColor)
    {
        _senderColor = senderColor;
        return this;
    }

    public EventMessageBuilder WithBody(string body)
    {
        _body = body;
        return this;
    }

    public EventMessageBuilder WithSentAt(DateTime sentAt)
    {
        _sentAt = sentAt;
        return this;
    }

    public EventMessage Build() => new()
    {
        EventId = _eventId,
        ParticipantId = _participantId,
        SenderDisplayName = _senderDisplayName,
        SenderColor = _senderColor,
        Body = _body,
        SentAt = _sentAt
    };
}
