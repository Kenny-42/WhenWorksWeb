using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>
    /// Returns the participant authorized for the current browser and event, if present.
    /// </summary>
    private async Task<Participant?> GetAuthorizedParticipantAsync(Event eventEntity, CancellationToken cancellationToken)
    {
        var cookieName = GetEventAccessCookieName(eventEntity.Code);

        // If the event access cookie is not present in the request, return null to indicate that no authorized participant was found.
        if (!Request.Cookies.TryGetValue(cookieName, out var protectedValue))
        {
            return null;
        }

        // If the event access cookie is present, attempt to unprotect and parse its value to identify the participant it corresponds to.
        try
        {
            var unprotectedValue = _eventAccessProtector.Unprotect(protectedValue);
            var parts = unprotectedValue.Split('|', 2);

            if (parts.Length != 2)
            {
                return null;
            }

            if (!int.TryParse(parts[1], out var participantId))
            {
                return null;
            }

            return await _db.Participants
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    p => p.Id == participantId && p.EventId == eventEntity.Id,
                    cancellationToken);
        }
        // If unprotecting the cookie value fails due to tampering or corruption, catch the exception, delete the invalid cookie,
        // and return null.
        catch (CryptographicException)
        {
            Response.Cookies.Delete(cookieName);
            return null;
        }
    }

    /// <summary>
    /// Returns the participant currently associated with the event for this browser or signed-in user.
    /// </summary>
    /// <remarks>The access cookie is checked first. If no cookie participant exists and user fallback is enabled, the method
    /// tries to load a single participant for the signed-in user in the same event.</remarks>
    private async Task<Participant?> GetCurrentParticipantAsync(
        Event eventEntity,
        ApplicationUser? currentUser,
        bool includeUserFallback,
        CancellationToken cancellationToken)
    {
        var authorizedParticipant = await GetAuthorizedParticipantAsync(eventEntity, cancellationToken);
        if (authorizedParticipant is not null)
        {
            return authorizedParticipant;
        }

        if (!includeUserFallback || currentUser is null)
        {
            return null;
        }

        // Load participants for the current user without AsNoTracking so a missing rejoin code can be generated and saved.
        var existingParticipantsForUser = await _db.Participants
            .Where(p => p.EventId == eventEntity.Id && p.UserId == currentUser.Id)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);

        // If exactly one participant exists for the signed-in user, use it to pre-populate the form.
        if (existingParticipantsForUser.Count == 1)
        {
            var existingParticipant = existingParticipantsForUser[0];

            // If the existing participant record does not have a rejoin code, generate and save one so the user
            // can use it if they sign out and need to rejoin.
            if (string.IsNullOrWhiteSpace(existingParticipant.RejoinCode))
            {
                existingParticipant.RejoinCode = await _codeGenerator.GenerateUniqueParticipantRejoinCodeAsync(cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return existingParticipant;
        }

        return null;
    }

    /// <summary>
    /// Stores a browser cookie that grants access to the event home page for the signed-in participant.
    /// </summary>
    private void SetEventAccessCookie(Event eventEntity, Participant participant)
    {
        // Build the cookie name using the event code to ensure it's unique per event.
        // The cookie value is a protected string that includes the event code and participant ID.
        var cookieName = GetEventAccessCookieName(eventEntity.Code);

        // Protect the cookie value using the data protector to prevent tampering. The value includes the event code and participant ID,
        // which will be used later to identify the participant when they access the event home page.
        var protectedValue = _eventAccessProtector.Protect($"{eventEntity.Code.ToUpperInvariant()}|{participant.Id}");

        // Set the event access cookie with an application-wide path so it can be read on both the sign-in page and the event home page.
        Response.Cookies.Append(cookieName, protectedValue, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    /// <summary>
    /// Builds the event access cookie name for a specific event code.
    /// </summary>
    private static string GetEventAccessCookieName(string code)
    {
        // The cookie name is constructed by combining a fixed prefix with the uppercase event code to ensure uniqueness and consistency.
        return $"{EventAccessCookiePrefix}{code.ToUpperInvariant()}";
    }
}
