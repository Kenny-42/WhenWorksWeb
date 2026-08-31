using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Data;
using WhenWorksWeb.Models;
using WhenWorksWeb.Services;

namespace WhenWorksWeb.Controllers;

/// <summary>
/// Provides access to the My Events page for authenticated users.
/// </summary>
/// <param name="db">The database context used to read and persist events and participants.</param>
/// <param name="userManager">Resolves the currently signed-in application user.</param>
/// <param name="eventDateCleanup">Removes now-empty candidate dates after a participant's availability marks are deleted.</param>
[Authorize]
public class MyEventsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, EventDateCleanupService eventDateCleanup) : Controller
{
    /// <summary>The number of events displayed per page on the My Events list.</summary>
    private const int PageSize = 6;

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

        // The WHERE filter (events this user created or joined) still runs in the database — only the
        // ordering and paging below happen client-side, and only over this already-small, per-user set (see
        // CODING_CONVENTIONS.md's Performance section, which calls this list out by name as the case that
        // doesn't need unbounded-growth-style query pagination). The participant details are pulled with
        // correlated subqueries so this doesn't need a second merge step.
        var matchingEvents = await db.Events
            .AsNoTracking()
            .Where(e => e.CreatedByUserId == currentUserId || e.Participants.Any(p => p.UserId == currentUserId))
            .Select(e => new
            {
                e.Id,
                e.Code,
                e.Title,
                e.CreatedByUserId,
                e.LastActiveAt,
                Emoji = e.Settings != null ? e.Settings.Emoji : ModelConstants.DefaultEventEmoji,
                Description = e.Settings != null ? e.Settings.Description : null,
                TotalParticipantCount = e.Participants.Count(),
                Participants = e.Participants
                    .Where(p => p.UserId == currentUserId)
                    .Select(p => new { p.Id, p.DisplayName, p.Color })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        // Ordered client-side rather than via the database (SQLite's provider refuses to translate ORDER BY
        // on a DateTimeOffset expression at all — works fine on SQL Server, but the test suite runs against
        // real SQLite per CODING_CONVENTIONS.md, and no translatable derived expression, e.g. .Ticks, was
        // found either). Ordered by most recently updated, then by code to break ties.
        var orderedEvents = matchingEvents
            .OrderByDescending(e => e.LastActiveAt)
            .ThenBy(e => e.Code, StringComparer.Ordinal)
            .ToList();

        var totalCount = orderedEvents.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

        // Clamp the requested page into the valid range instead of erroring on an out-of-range value (e.g. a stale
        // bookmark to a page that no longer exists after events were deleted).
        var currentPage = Math.Clamp(page, 1, totalPages);

        var events = orderedEvents
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize);

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
                Description = string.IsNullOrWhiteSpace(myEvent.Description) ? ModelConstants.DefaultEventDescription : myEvent.Description,
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

                // Remove this participant's availability marks first — that FK is NoAction (not
                // cascade) to avoid a multiple-cascade-paths conflict with EventDate's own
                // cascade into the same table (see ApplicationDbContext), so it isn't cleaned up
                // automatically the way EventMessages is above.
                await db.ParticipantAvailabilities
                    .Where(a => a.ParticipantId == participantEntity.Id)
                    .ExecuteDeleteAsync(cancellationToken);

                // ExecuteDeleteAsync writes straight to the database and never touches the change
                // tracker, so any ParticipantAvailability rows for this participant that happened
                // to already be tracked (e.g. loaded earlier in the same request) still look
                // intact to EF — removing the participant below would then trip "severed required
                // relationship" the moment EF notices their FK now points at nothing. Detach them
                // explicitly rather than relying on nothing having tracked them.
                foreach (var trackedMark in db.ChangeTracker.Entries<ParticipantAvailability>()
                             .Where(e => e.Entity.ParticipantId == participantEntity.Id)
                             .ToList())
                {
                    trackedMark.State = EntityState.Detached;
                }

                // The marks just removed above may have been the last ones on some of this
                // event's candidate dates — clean those up too rather than leaving empty
                // EventDate rows behind.
                await eventDateCleanup.RemoveEmptyDatesAsync(eventId, cancellationToken);

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
