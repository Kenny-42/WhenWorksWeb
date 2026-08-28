using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>The number of participants displayed per page on the event's People tab.</summary>
    private const int PeoplePageSize = 8;

    /// <summary>
    /// Displays the People tab for the event associated with the specified code: the participant
    /// roster, organizers first then alphabetically by display name, 8 per page.
    /// </summary>
    /// <remarks>The page is only accessible after a successful sign-in flow has issued a valid access cookie.</remarks>
    [HttpGet("/event/{code}/people", Name = "EventPeople")]
    public async Task<IActionResult> People(string code, CancellationToken cancellationToken, int page = 1)
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

        var totalCount = await _db.Participants
            .AsNoTracking()
            .CountAsync(p => p.EventId == eventEntity.Id, cancellationToken);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PeoplePageSize));

        // Clamp the requested page into the valid range instead of erroring on an out-of-range value.
        var currentPage = Math.Clamp(page, 1, totalPages);

        var currentParticipantId = participant.Id;

        // Organizers first, then alphabetically by display name (ties within the organizer group
        // are also alphabetical) — not creation order.
        var participants = await _db.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventEntity.Id)
            .OrderByDescending(p => p.IsOrganizer)
            .ThenBy(p => p.DisplayName)
            .Skip((currentPage - 1) * PeoplePageSize)
            .Take(PeoplePageSize)
            .Select(p => new EventPersonViewModel
            {
                DisplayName = p.DisplayName,
                Color = p.Color,
                IsOrganizer = p.IsOrganizer,
                IsCurrentParticipant = p.Id == currentParticipantId
            })
            .ToListAsync(cancellationToken);

        return View(new EventPeopleViewModel
        {
            Header = BuildEventHeader(eventEntity, emoji, EventTab.People),
            Participants = participants,
            CurrentPage = currentPage,
            TotalPages = totalPages,
            TotalCount = totalCount,
            PageSize = PeoplePageSize
        });
    }
}
