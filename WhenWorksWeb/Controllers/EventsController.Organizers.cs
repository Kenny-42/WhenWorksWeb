using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

/// <summary>
/// The Settings tab's "Organizer permissions" card actions: promoting a participant to organizer,
/// demoting an organizer back to a regular participant, toggling another organizer's
/// <see cref="Participant.CanManageOrganizers"/> flag, and reporting the live organizer count the
/// "demote the last organizer" confirmation modal checks before showing itself. Split out from
/// <c>EventsController.Settings.cs</c> since that file was already sizeable before these
/// actions — see the Controller section of the organizer-permissions feature spec.
/// </summary>
public partial class EventsController
{
    /// <summary>
    /// Promotes a participant to organizer. Requires <see cref="Participant.CanManageOrganizers"/>
    /// (re-checked against the database, never trusted from the request); the promoted participant
    /// starts with <see cref="Participant.CanManageOrganizers"/> false, per the Permission Model's
    /// defaults.
    /// </summary>
    [HttpPost("/event/{code}/settings/organizers/promote", Name = "EventPromoteOrganizer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteOrganizer(string code, [FromForm] int participantId, CancellationToken cancellationToken)
    {
        var (context, failure) = await AuthorizeEventActionAsync(code, CanManageOrganizersAsync, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var (eventEntity, _) = context!.Value;

        var target = await _db.Participants
            .SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == eventEntity.Id, cancellationToken);

        // A stale dropdown (already promoted by someone else, or a tampered participantId from
        // outside this event) is a no-op rather than an error — the redirect back to Settings
        // shows the current, correct state either way.
        if (target is not null && !target.IsOrganizer)
        {
            target.IsOrganizer = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return RedirectToRoute("EventSettings", new { code = eventEntity.Code });
    }

    /// <summary>
    /// Demotes an organizer back to a regular participant, clearing both
    /// <see cref="Participant.IsOrganizer"/> and <see cref="Participant.CanManageOrganizers"/>.
    /// Requires <see cref="Participant.CanManageOrganizers"/>; a participant who holds it can
    /// select their own name to step down, which is how self-demotion is reached — there's no
    /// separate self-service control outside this card.
    /// </summary>
    [HttpPost("/event/{code}/settings/organizers/demote", Name = "EventDemoteOrganizer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DemoteOrganizer(string code, [FromForm] int participantId, CancellationToken cancellationToken)
    {
        var (context, failure) = await AuthorizeEventActionAsync(code, CanManageOrganizersAsync, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var (eventEntity, _) = context!.Value;

        var target = await _db.Participants
            .SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == eventEntity.Id, cancellationToken);

        if (target is not null && target.IsOrganizer)
        {
            target.IsOrganizer = false;
            target.CanManageOrganizers = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return RedirectToRoute("EventSettings", new { code = eventEntity.Code });
    }

    /// <summary>
    /// Toggles another organizer's <see cref="Participant.CanManageOrganizers"/> flag ("Full
    /// access" in the UI). Requires <see cref="Participant.CanManageOrganizers"/> itself. Returns
    /// just the affected pill (<c>_OrganizerPill</c>) for the Settings page's fetch-and-swap
    /// enhancement, same <c>X-Requested-With</c> convention as <see cref="People"/>; falls back to
    /// a full redirect for a plain form post with JS unavailable. Takes the intended end state as
    /// <paramref name="desiredValue"/> rather than blindly negating the current value server-side,
    /// so the action is idempotent: if the client's fetch fails after this already succeeded (e.g.
    /// the response never arrives) and it falls back to resubmitting the same form, the second
    /// request sets the same value again instead of flipping the flag back to its original state.
    /// </summary>
    [HttpPost("/event/{code}/settings/organizers/{participantId:int}/toggle", Name = "EventToggleCanManageOrganizers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCanManageOrganizers(string code, int participantId, [FromForm] bool desiredValue, CancellationToken cancellationToken)
    {
        var (context, failure) = await AuthorizeEventActionAsync(code, CanManageOrganizersAsync, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var (eventEntity, participant) = context!.Value;

        var target = await _db.Participants
            .SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == eventEntity.Id, cancellationToken);

        // Only a current organizer can hold the flag — a stale pill (e.g. the target was demoted
        // by someone else a moment ago) is rejected rather than silently flipping a flag that
        // wouldn't mean anything on a non-organizer.
        if (target is null || !target.IsOrganizer)
        {
            return NotFound();
        }

        target.CanManageOrganizers = desiredValue;
        await _db.SaveChangesAsync(cancellationToken);

        if (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.Ordinal))
        {
            var pillModel = new EventOrganizerViewModel
            {
                EventCode = eventEntity.Code,
                Id = target.Id,
                DisplayName = target.DisplayName,
                Color = target.Color,
                CanManageOrganizers = target.CanManageOrganizers,
                IsCurrentParticipant = target.Id == participant.Id
            };

            return PartialView("_OrganizerPill", pillModel);
        }

        return RedirectToRoute("EventSettings", new { code = eventEntity.Code });
    }

    /// <summary>
    /// Reports the event's current organizer count as JSON. Settings.cshtml's demote form calls
    /// this immediately before submitting to decide, off a live number rather than the page's
    /// load-time snapshot, whether demoting would leave the event with zero organizers and the
    /// "last organizer" confirmation modal should show — a page left open in one tab while another
    /// organizer is demoted (or promoted) elsewhere would otherwise warn (or fail to warn) based on
    /// a stale count. Read-only, so unlike this file's other actions it doesn't need
    /// <see cref="AuthorizeEventActionAsync"/>'s tracked-event/mutation setup, but still requires a
    /// signed-in participant with <see cref="Participant.CanManageOrganizers"/> — the same
    /// audience who can see the demote form that calls it.
    /// </summary>
    [HttpGet("/event/{code}/settings/organizers/count", Name = "EventOrganizerCount")]
    public async Task<IActionResult> GetOrganizerCount(string code, CancellationToken cancellationToken)
    {
        var eventEntity = await GetEventAsync(code, cancellationToken);
        if (eventEntity is null)
        {
            return NotFound();
        }

        var participant = await GetCurrentParticipantAsync(eventEntity, currentUser: null, includeUserFallback: false, cancellationToken);
        if (participant is null || !await CanManageOrganizersAsync(eventEntity, participant, cancellationToken))
        {
            return Forbid();
        }

        var organizerCount = await _db.Participants
            .AsNoTracking()
            .CountAsync(p => p.EventId == eventEntity.Id && p.IsOrganizer, cancellationToken);

        return Json(new { count = organizerCount });
    }

    /// <summary>
    /// Loads the data the "Organizer permissions" card needs: every current organizer, alphabetical
    /// by display name (pill list + Demote dropdown — there's no "organizers first" tiering here
    /// the way the People tab has, since every row in this list already is an organizer), and every
    /// current non-organizer participant, same ordering (Promote dropdown). Only called when the
    /// current participant holds <see cref="Participant.CanManageOrganizers"/> — see
    /// <see cref="BuildEventSettingsViewModelAsync"/>.
    /// </summary>
    private async Task<(IReadOnlyList<EventOrganizerViewModel> Organizers, IReadOnlyList<EventParticipantOptionViewModel> PromotableParticipants)> BuildOrganizerManagementDataAsync(
        Event eventEntity, Participant currentParticipant, CancellationToken cancellationToken)
    {
        var currentParticipantId = currentParticipant.Id;

        var organizers = await _db.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventEntity.Id && p.IsOrganizer)
            .OrderBy(p => p.DisplayName)
            .Select(p => new EventOrganizerViewModel
            {
                EventCode = eventEntity.Code,
                Id = p.Id,
                DisplayName = p.DisplayName,
                Color = p.Color,
                CanManageOrganizers = p.CanManageOrganizers,
                IsCurrentParticipant = p.Id == currentParticipantId
            })
            .ToListAsync(cancellationToken);

        var promotableParticipants = await _db.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventEntity.Id && !p.IsOrganizer)
            .OrderBy(p => p.DisplayName)
            .Select(p => new EventParticipantOptionViewModel
            {
                Id = p.Id,
                DisplayName = p.DisplayName
            })
            .ToListAsync(cancellationToken);

        return (organizers, promotableParticipants);
    }
}
