using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Data;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

/// <summary>
/// Provides access to the My Events page for authenticated users.
/// </summary>
[Authorize]
public class MyEventsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public MyEventsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    /// <summary>
    /// This action retrieves the list of events the current user has joined or created and displays them on the My Events page. 
    /// The events are ordered alphabetically by title and then by code. Each event's emoji is also included for display. 
    /// If the user is not authenticated, they will be challenged to log in.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Only allow access to this action if the user is authenticated.
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Challenge();
        }

        var currentUserId = currentUser.Id;

        // Load all events the current user has joined or created in a single query shape.
        // The participant details are pulled with correlated subqueries so the page does not need a second merge step.
        var events = await _db.Events
            .AsNoTracking()
            .Where(e => e.CreatedByUserId == currentUserId || e.Participants.Any(p => p.UserId == currentUserId))
            .Select(e => new
            {
                e.Id,
                e.Code,
                e.Title,
                e.CreatedByUserId,
                Emoji = e.Settings != null ? e.Settings.Emoji : ModelConstants.DefaultEventEmoji,
                Participants = e.Participants
                    .Where(p => p.UserId == currentUserId)
                    .Select(p => new { p.Id, p.DisplayName })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // Map the events to the view model, ordering them by title and then by code for display on the My Events page.
        var viewModel = events
            .OrderBy(myEvent => myEvent.Title)
            .ThenBy(myEvent => myEvent.Code)
            .Select(myEvent => new MyEventViewModel
            {
                EventId = myEvent.Id,
                Participants = myEvent.Participants
                    .Select(p => new MyEventParticipantViewModel
                    {
                        ParticipantId = p.Id,
                        DisplayName = p.DisplayName
                    })
                    .ToList(),
                CreatedByUserId = myEvent.CreatedByUserId,
                Code = myEvent.Code,
                Title = myEvent.Title,
                Emoji = myEvent.Emoji,
                SignInUrl = Url.RouteUrl("EventSignIn", new { code = myEvent.Code }) ?? string.Empty
            })
            .ToList();

        return View(viewModel);
    }

    /// <summary>
    /// Deletes either the current user's participant record for an event or the entire event.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int eventId, int? participantId, string deleteMode, CancellationToken cancellationToken)
    {
        // Only allow access to this action if the user is authenticated.
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Challenge();
        }

        // Normalize the deleteMode parameter to allow for case-insensitive matching and trim whitespace.
        var normalizedDeleteMode = deleteMode?.Trim().ToLowerInvariant();

        // Handle deletion based on the specified mode: "event" for deleting the entire event, "participant" for leaving the event.
        if (normalizedDeleteMode == "event")
        {
            var eventEntity = await _db.Events
                .SingleOrDefaultAsync(e => e.Id == eventId, cancellationToken);

            // Only the creator of the event can delete it, so check if the current user is the creator.
            if (eventEntity is null)
            {
                return NotFound();
            }

            // Use ordinal string comparison for user ID to ensure exact match without culture-specific rules.
            if (!string.Equals(eventEntity.CreatedByUserId, currentUser.Id, StringComparison.Ordinal))
            {
                return Forbid();
            }

            _db.Events.Remove(eventEntity);

            await _db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Index));
        }

        // Handle participant deletion (leaving the event).
        if (normalizedDeleteMode == "participant")
        {
            if (participantId is null)
            {
                return BadRequest();
            }

            using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            // Verify that the participant record exists and belongs to the current user for the specified event.
            try
            {
                var participantEntity = await _db.Participants
                    .SingleOrDefaultAsync(
                        p => p.Id == participantId.Value &&
                             p.EventId == eventId &&
                             p.UserId == currentUser.Id,
                        cancellationToken);

                if (participantEntity is null)
                {
                    return NotFound();
                }

                // Set the ParticipantId to null for all messages associated with this participant to preserve
                // message history without orphaned foreign keys.
                await _db.EventMessages
                    .Where(m => m.ParticipantId == participantEntity.Id)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(m => m.ParticipantId, (int?)null),
                        cancellationToken);

                _db.Participants.Remove(participantEntity);
                await _db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        return BadRequest();
    }
}