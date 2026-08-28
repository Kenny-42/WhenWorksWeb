using Microsoft.AspNetCore.Mvc;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>
    /// Displays the Settings tab for the event associated with the specified code.
    /// </summary>
    /// <remarks>The page is only accessible after a successful sign-in flow has issued a valid access cookie.
    /// The actual "Shape the plan"/"Call the date"/Delete Event cards are a separate follow-up; this action
    /// currently only serves the shared page chrome and the current participant's permission flag.</remarks>
    [HttpGet("/event/{code}/settings", Name = "EventSettings")]
    public async Task<IActionResult> Settings(string code, CancellationToken cancellationToken)
    {
        var eventEntity = await GetEventAsync(code, cancellationToken);
        if (eventEntity is null)
        {
            return CreateEventNotFoundResult();
        }

        var participant = await GetCurrentParticipantAsync(eventEntity, currentUser: null, includeUserFallback: false, cancellationToken);
        if (participant is null)
        {
            return RedirectToRoute("EventSignIn", new { code = eventEntity.Code });
        }

        var emoji = await GetEventEmojiAsync(eventEntity, cancellationToken);
        var canManageEvent = await CanManageEventAsync(eventEntity, participant, cancellationToken);

        return View(new EventSettingsViewModel
        {
            Header = BuildEventHeader(eventEntity, emoji, EventTab.Settings),
            CanManageEvent = canManageEvent
        });
    }
}
