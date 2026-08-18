using Microsoft.AspNetCore.Mvc;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

public partial class EventsController
{
    /// <summary>
    /// Displays the event home page for the event associated with the specified code.
    /// </summary>
    /// <remarks>The page is only accessible after a successful sign-in flow has issued a valid access cookie.</remarks>
    [HttpGet("/event/{code}", Name = "EventHome")]
    public async Task<IActionResult> Home(string code, CancellationToken cancellationToken)
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

        return View(new EventHomeViewModel
        {
            Code = eventEntity.Code,
            Title = eventEntity.Title
        });
    }
}
