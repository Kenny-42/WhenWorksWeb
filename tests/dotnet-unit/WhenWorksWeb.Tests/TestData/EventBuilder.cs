using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.TestData;

/// <summary>
/// Hand-rolled test data builder for <see cref="Event"/> (see CODING_CONVENTIONS.md's Testing Conventions
/// for why builders rather than AutoFixture/Bogus). Defaults are valid per the real schema constraints
/// (6-character code drawn from the real alphabet, non-empty title) so a test only needs to override the
/// specific field it cares about.
/// </summary>
public sealed class EventBuilder
{
    private string _code = "BCDFGH";
    private string _title = "Test Event";
    private string? _createdByUserId;

    public EventBuilder WithCode(string code)
    {
        _code = code;
        return this;
    }

    public EventBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public EventBuilder WithCreatedByUserId(string? userId)
    {
        _createdByUserId = userId;
        return this;
    }

    public Event Build() => Event.Create(_code, _title, _createdByUserId);
}
