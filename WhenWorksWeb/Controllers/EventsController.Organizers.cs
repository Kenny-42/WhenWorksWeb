using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

/// <summary>
/// The Settings tab's "Organizer permissions" card actions: promoting a participant to organizer,
/// demoting an organizer back to a regular participant, and toggling another organizer's
/// <see cref="Participant.CanManageOrganizers"/> flag. Split out from
/// <c>EventsController.Settings.cs</c> since that file was already sizeable before these three
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
        var eventEntity = await GetTrackedEventAsync(code, cancellationToken);
        if (eventEntity is null)
        {
            return CreateEventNotFoundResult();
        }

        var participant = await GetCurrentParticipantAsync(eventEntity, currentUser: null, includeUserFallback: false, cancellationToken);
        if (participant is null)
        {
            return RedirectToRoute("EventSignIn", new { code = eventEntity.Code });
        }

        if (!await CanManageOrganizersAsync(eventEntity, participant, cancellationToken))
        {
            return Forbid();
        }

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
        var eventEntity = await GetTrackedEventAsync(code, cancellationToken);
        if (eventEntity is null)
        {
            return CreateEventNotFoundResult();
        }

        var participant = await GetCurrentParticipantAsync(eventEntity, currentUser: null, includeUserFallback: false, cancellationToken);
        if (participant is null)
        {
            return RedirectToRoute("EventSignIn", new { code = eventEntity.Code });
        }

        if (!await CanManageOrganizersAsync(eventEntity, participant, cancellationToken))
        {
            return Forbid();
        }

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
    /// a full redirect for a plain form post with JS unavailable.
    /// </summary>
    [HttpPost("/event/{code}/settings/organizers/{participantId:int}/toggle", Name = "EventToggleCanManageOrganizers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCanManageOrganizers(string code, int participantId, CancellationToken cancellationToken)
    {
        var eventEntity = await GetTrackedEventAsync(code, cancellationToken);
        if (eventEntity is null)
        {
            return CreateEventNotFoundResult();
        }

        var participant = await GetCurrentParticipantAsync(eventEntity, currentUser: null, includeUserFallback: false, cancellationToken);
        if (participant is null)
        {
            return RedirectToRoute("EventSignIn", new { code = eventEntity.Code });
        }

        if (!await CanManageOrganizersAsync(eventEntity, participant, cancellationToken))
        {
            return Forbid();
        }

        var target = await _db.Participants
            .SingleOrDefaultAsync(p => p.Id == participantId && p.EventId == eventEntity.Id, cancellationToken);

        // Only a current organizer can hold the flag — a stale pill (e.g. the target was demoted
        // by someone else a moment ago) is rejected rather than silently flipping a flag that
        // wouldn't mean anything on a non-organizer.
        if (target is null || !target.IsOrganizer)
        {
            return NotFound();
        }

        target.CanManageOrganizers = !target.CanManageOrganizers;
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
