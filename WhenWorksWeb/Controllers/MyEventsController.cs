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
/// <param name="db">The database context used to read and persist events and participants.</param>
/// <param name="userManager">Resolves the currently signed-in application user.</param>
[Authorize]
public class MyEventsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    /// <summary>The number of events displayed per page on the My Events list.</summary>
    private const int PageSize = 6;

    /// <summary>Shown in place of an event's description when the organizer hasn't set one.</summary>
    private const string DefaultEventDescription = "A new plan is taking shape.";

    /// <summary>
    /// This action retrieves one page of the events the current user has joined or created and displays them on the
    /// My Events page. The events are ordered by most recently updated (LastActiveAt, which is set to the creation
    /// time when an event is first created and bumped on every later modification), then by code to break ties.
    /// Each event's emoji and description are also included for display, along with a total participant count
    /// across all users.
    /// If the user is not authenticated, they will be challenged to log in.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the database query.</param>
    /// <param name="page">The 1-based page number to display; clamped into the valid range rather than erroring.</param>
    /// <returns>The My Events view, or a challenge result if the user is not authenticated.</returns>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken, int page = 1)
    {
        var currentUser = await GetAuthenticatedUserAsync();
        if (currentUser is null)
        {
            return Challenge();
        }

        var currentUserId = currentUser.Id;

        // Base query for events the current user created or has joined, shared between the total count below and
        // the paged slice further down so both agree on exactly the same set of events.
        var baseQuery = db.Events
            .AsNoTracking()
            .Where(e => e.CreatedByUserId == currentUserId || e.Participants.Any(p => p.UserId == currentUserId));

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

        // Clamp the requested page into the valid range instead of erroring on an out-of-range value (e.g. a stale
        // bookmark to a page that no longer exists after events were deleted).
        var currentPage = Math.Clamp(page, 1, totalPages);

        // Load one page of events. The participant details are pulled with correlated subqueries so the page does
        // not need a second merge step.
        var events = await baseQuery
            .OrderByDescending(e => e.LastActiveAt)
            .ThenBy(e => e.Code)
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(e => new
            {
                e.Id,
                e.Code,
                e.Title,
                e.CreatedByUserId,
                Emoji = e.Settings != null ? e.Settings.Emoji : ModelConstants.DefaultEventEmoji,
                Description = e.Settings != null ? e.Settings.Description : null,
                TotalParticipantCount = e.Participants.Count(),
                Participants = e.Participants
                    .Where(p => p.UserId == currentUserId)
                    .Select(p => new { p.Id, p.DisplayName, p.Color })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // Map the events to the view model for display on the My Events page.
        var viewModel = events
            .Select(myEvent => new MyEventViewModel
            {
                EventId = myEvent.Id,
                Participants = myEvent.Participants
                    .Select(p => new MyEventParticipantViewModel
                    {
                        ParticipantId = p.Id,
                        DisplayName = p.DisplayName,
                        Color = p.Color
                    })
                    .ToList(),
                CreatedByUserId = myEvent.CreatedByUserId,
                Code = myEvent.Code,
                Title = myEvent.Title,
                Emoji = myEvent.Emoji,
                Description = string.IsNullOrWhiteSpace(myEvent.Description) ? DefaultEventDescription : myEvent.Description,
                TotalParticipantCount = myEvent.TotalParticipantCount,
                SignInUrl = Url.RouteUrl("EventSignIn", new { code = myEvent.Code }) ?? string.Empty
            })
            .ToList();

        return View(new MyEventsPageViewModel
        {
            Events = viewModel,
            CurrentPage = currentPage,
            TotalPages = totalPages,
            TotalCount = totalCount,
            PageSize = PageSize
        });
    }

    /// <summary>
    /// Deletes either the current user's participant record for an event or the entire event.
    /// </summary>
    /// <param name="eventId">The id of the event to delete or leave.</param>
    /// <param name="participantId">The id of the current user's participant record, required when <paramref name="deleteMode"/> is "participant".</param>
    /// <param name="deleteMode">Either "event" to delete the entire event, or "participant" to leave it.</param>
    /// <param name="cancellationToken">Token used to cancel the database operations.</param>
    /// <param name="page">The My Events page the user deleted from, so the redirect lands back on the same page.</param>
    /// <returns>A redirect to the My Events list on success, or an error result otherwise.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int eventId, int? participantId, string deleteMode, CancellationToken cancellationToken, int page = 1)
    {
        var currentUser = await GetAuthenticatedUserAsync();
        if (currentUser is null)
        {
            return Challenge();
        }

        // Normalize the deleteMode parameter to allow for case-insensitive matching and trim whitespace.
        var normalizedDeleteMode = deleteMode?.Trim().ToLowerInvariant();

        // Handle deletion based on the specified mode: "event" for deleting the entire event, "participant" for leaving the event.
        if (normalizedDeleteMode == "event")
        {
            var eventEntity = await db.Events
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

            db.Events.Remove(eventEntity);

            await db.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Index), new { page });
        }

        // Handle participant deletion (leaving the event).
        if (normalizedDeleteMode == "participant")
        {
            if (participantId is null)
            {
                return BadRequest();
            }

            using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Verify that the participant record exists and belongs to the current user for the specified event.
            try
            {
                var participantEntity = await db.Participants
                    .SingleOrDefaultAsync(
                        p => p.Id == participantId.Value &&
                             p.EventId == eventId &&
                             p.UserId == currentUser.Id,
                        cancellationToken);

                if (participantEntity is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return NotFound();
                }

                // Set the ParticipantId to null for all messages associated with this participant to preserve
                // message history without orphaned foreign keys.
                await db.EventMessages
                    .Where(m => m.ParticipantId == participantEntity.Id)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(m => m.ParticipantId, (int?)null),
                        cancellationToken);

                db.Participants.Remove(participantEntity);
                await db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return RedirectToAction(nameof(Index), new { page });
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        return BadRequest();
    }

    /// <summary>
    /// Returns the currently authenticated application user, or null if no user is signed in.
    /// </summary>
    private async Task<ApplicationUser?> GetAuthenticatedUserAsync()
    {
        return await userManager.GetUserAsync(User);
    }
}
