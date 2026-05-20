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
    /// This action retrieves the list of events the current user has joined and displays them on the My Events page. 
    /// The events are ordered alphabetically by title and then by code. Each event's emoji is also included for display. 
    /// If the user is not authenticated, they will be challenged to log in.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Challenge();
        }

        // Load the user's joined events through Participant records.
        // Event.Settings is included so the emoji can be displayed without extra queries.
        var participants = await _db.Participants
            .AsNoTracking()
            .Where(p => p.UserId == currentUser.Id)
            .Include(p => p.Event)
                .ThenInclude(e => e.Settings)
            .ToListAsync(cancellationToken);

        var viewModel = participants
            .GroupBy(p => p.EventId)
            .Select(group => group.First())
            .OrderBy(p => p.Event.Title)
            .ThenBy(p => p.Event.Code)
            .Select(participant => new MyEventViewModel
            {
                Code = participant.Event.Code,
                Title = participant.Event.Title,
                Emoji = participant.Event.Settings?.Emoji ?? ModelConstants.DefaultEventEmoji,
                SignInUrl = Url.RouteUrl("EventSignIn", new { code = participant.Event.Code }) ?? string.Empty
            })
            .ToList();

        return View(viewModel);
    }
}