using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Data;
using WhenWorksWeb.Models;
using WhenWorksWeb.Services;

namespace WhenWorksWeb.Controllers;

/// <summary>
/// Provides actions for creating, joining, and signing in to events within the application.
/// </summary>
public class EventsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly EventCodeGenerator _codeGenerator;
    private readonly UserManager<ApplicationUser> _userManager;

    public EventsController(
        ApplicationDbContext db,
        EventCodeGenerator codeGenerator,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _codeGenerator = codeGenerator;
        _userManager = userManager;
    }

    /// <summary>
    /// Handles HTTP POST requests to create a new event using the provided view model data.
    /// </summary>
    /// <remarks>If the submitted event name is empty or the model state is invalid, the method redisplays the
    /// form with validation messages. On successful creation, the user is redirected to the sign-in page for the new
    /// event.</remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IndexViewModel model)
    {
        // Remove the EventCode from model state validation since it's not relevant for event creation and may be empty.
        ModelState.Remove(nameof(IndexViewModel.EventCode));

        // Trim whitespace from the event name to ensure consistent validation and storage.
        // This prevents issues with names that are only whitespace or have leading/trailing spaces.
        model.CreateEventName = model.CreateEventName?.Trim();

        // Validate the model state after adjusting the CreateEventName.
        // If it's invalid (e.g., empty), redisplay the form with validation errors.
        if (!ModelState.IsValid)
        {
            return View("~/Views/Home/Index.cshtml", model);
        }

        // Generate a unique event code using the EventCodeGenerator service.
        var code = await _codeGenerator.GenerateUniqueCodeAsync();
        // Create a new Event entity using the generated code and the provided event name from the view model.
        var eventEntity = Event.Create(code, model.CreateEventName!);

        // Add the new event entity to the database context and save changes to persist it in the database.
        _db.Events.Add(eventEntity);
        await _db.SaveChangesAsync();

        // Redirect the user to the event sign-in page for the newly created event using its unique code.
        return RedirectToRoute("EventSignIn", new { code = eventEntity.Code });
    }

    /// <summary>
    /// Processes a join request for an event based on the provided event code and redirects the user to the event
    /// sign-in page if the event exists.
    /// </summary>
    /// <remarks>If the event code does not match any existing event, a model error is added and the user
    /// remains on the home page. The event code is normalized by trimming whitespace and converting to uppercase before
    /// lookup.</remarks>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(IndexViewModel model)
    {
        // Remove the CreateEventName from model state validation since it's not relevant for joining an event and may be empty.
        ModelState.Remove(nameof(IndexViewModel.CreateEventName));

        // Normalize the event code by trimming whitespace and converting it to uppercase to ensure consistent lookup and visuals.
        model.EventCode = model.EventCode?.Trim().ToUpperInvariant();

        // Validate the model state after adjusting the EventCode.
        // If it's invalid (e.g., empty), redisplay the form with validation errors.
        if (!ModelState.IsValid)
        {
            return View("~/Views/Home/Index.cshtml", model);
        }

        // Query the database for an event that matches the normalized event code.
        // AsNoTracking is used since we only need read access to check for existence.
        var eventEntity = await _db.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Code == model.EventCode);

        // If no event is found with the provided code, add a model error to inform the user and redisplay the form.
        if (eventEntity is null)
        {
            ModelState.AddModelError(nameof(IndexViewModel.EventCode), "No event was found for that code.");
            return View("~/Views/Home/Index.cshtml", model);
        }

        // Redirect the user to the event sign-in page for the found event using its unique code.
        return RedirectToRoute("EventSignIn", new { code = eventEntity.Code });
    }

    /// <summary>
    /// Displays the sign-in page for the event associated with the specified event code.
    /// </summary>
    [HttpGet("/event/{code}/signin", Name = "EventSignIn")]
    public async Task<IActionResult> SignIn(string code)
    {
        // Validate that the event code is not null, empty, or whitespace. If it is, return a 404 Not Found response.
        if (string.IsNullOrWhiteSpace(code))
        {
            return NotFound();
        }

        // Query the database for the event that matches the code from the URL.
        // AsNoTracking is used because this action only needs read access to display the page.
        var eventEntity = await _db.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Code == code);

        // If no matching event exists, return a 404 response instead of showing the sign-in page.
        if (eventEntity is null)
        {
            return NotFound();
        }

        // Retrieve the list of existing participant display names for the event to populate the dropdown in the sign-in form.
        var existingDisplayNames = await _db.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventEntity.Id)
            .OrderBy(p => p.DisplayName)
            .Select(p => p.DisplayName)
            .ToListAsync();

        // Attempt to retrieve the currently authenticated user, if any, to pre-populate the sign-in form with their information.
        var user = await _userManager.GetUserAsync(User);

        string displayName = user?.DisplayName ?? string.Empty;
        string color = user?.Color ?? "ff66c4";
        string? selectedDisplayName = null;

        // If the current user is already registered for this event, load their saved participant details
        // so the sign-in form can be pre-populated with their existing display name and color.
        if (user is not null)
        {
            var existingParticipant = await _db.Participants
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.EventId == eventEntity.Id && p.UserId == user.Id);

            // When a matching participant record exists, use it as the default sign-in state.
            if (existingParticipant is not null)
            {
                displayName = existingParticipant.DisplayName;
                color = existingParticipant.Color;
                selectedDisplayName = existingParticipant.DisplayName;
            }
        }

        // Build the sign-in view model with the event details, any known participant defaults,
        // and a sorted list of existing display names so the UI can prefill and offer quick selection.
        var viewModel = new EventSignInViewModel
        {
            Code = eventEntity.Code.ToUpperInvariant(),
            EventName = eventEntity.Title,
            DisplayName = displayName,
            Color = color,
            SelectedExistingDisplayName = selectedDisplayName,
            ExistingDisplayNames = existingDisplayNames
                .Select(name => new SelectListItem { Value = name, Text = name })
                .ToList()
        };

        // Render the sign-in view using the populated view model.
        return View(viewModel);
    }
}
