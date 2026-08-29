using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>
    /// Displays the Finalize tab for the event associated with the specified code: the "Call the
    /// date" suggestions/add-date form and the "Final dates" list.
    /// </summary>
    /// <remarks>The page is only accessible after a successful sign-in flow has issued a valid access cookie.</remarks>
    [HttpGet("/event/{code}/finalize", Name = "EventFinalize")]
    public async Task<IActionResult> Finalize(string code, CancellationToken cancellationToken)
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

        return View(await BuildEventFinalizeViewModelAsync(eventEntity, participant, cancellationToken));
    }

    /// <summary>
    /// Builds the Finalize tab view model: shared chrome, the Availability tab's calendar data
    /// (reused so the "Suggestions" sub-list is ranked by the same shared best-bets script, no
    /// second query shape needed), and the organizer's current final date entries (also reused
    /// from the calendar's own <see cref="EventCalendarViewModel.FinalDates"/> rather than
    /// queried again here, so the two can't diverge).
    /// </summary>
    private async Task<EventFinalizeViewModel> BuildEventFinalizeViewModelAsync(Event eventEntity, Participant participant, CancellationToken cancellationToken)
    {
        var emoji = await GetEventEmojiAsync(eventEntity, cancellationToken);
        var canManageEvent = await CanManageEventAsync(eventEntity, participant, cancellationToken);
        var calendar = await BuildEventCalendarAsync(eventEntity, participant, cancellationToken);

        return new EventFinalizeViewModel
        {
            Header = BuildEventHeader(eventEntity, emoji, EventTab.Finalize),
            CanManageEvent = canManageEvent,
            Calendar = calendar,
            FinalDates = calendar.FinalDates
        };
    }
}
