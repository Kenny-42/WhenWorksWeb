using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Data;

namespace WhenWorksWeb.Services;

/// <summary>
/// Deletes candidate <see cref="Models.EventDate"/> rows that no longer have any participant
/// marked available on them, so removing an availability mark — whether by toggling it off or by
/// removing the participant who made it — never leaves an empty, purposeless candidate date
/// behind. Shared by <c>EventsController</c> (toggling availability) and
/// <c>MyEventsController</c> (removing a participant), the two places an availability mark can be
/// removed.
/// </summary>
/// <param name="dbContext">The database context used to find and remove empty candidate dates.</param>
public class EventDateCleanupService(ApplicationDbContext dbContext)
{
    /// <summary>
    /// Deletes every <see cref="Models.EventDate"/> for the given event that currently has zero
    /// <see cref="Models.ParticipantAvailability"/> rows, and returns how many were removed.
    /// </summary>
    public Task<int> RemoveEmptyDatesAsync(int eventId, CancellationToken cancellationToken)
    {
        return dbContext.EventDates
            .Where(d => d.EventId == eventId && !d.Availabilities.Any())
            .ExecuteDeleteAsync(cancellationToken);
    }
}
