namespace WhenWorksWeb.Models;

/// <summary>
/// View model for one option in the Settings tab's Promote dropdown — a participant who is not
/// currently an organizer. See <see cref="EventSettingsViewModel.PromotableParticipants"/>.
/// </summary>
public sealed class EventParticipantOptionViewModel
{
    /// <summary>The participant's id.</summary>
    public required int Id { get; init; }

    /// <summary>The participant's display name for this event.</summary>
    public required string DisplayName { get; init; }
}
