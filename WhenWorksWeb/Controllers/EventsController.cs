using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
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
    public async Task<IActionResult> Create(IndexViewModel model, CancellationToken cancellationToken)
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
        var code = await _codeGenerator.GenerateUniqueCodeAsync(cancellationToken);
        // Create a new Event entity using the generated code and the provided event name from the view model.
        var eventEntity = Event.Create(code, model.CreateEventName!);

        // Add the new event entity to the database context and save changes to persist it in the database.
        _db.Events.Add(eventEntity);
        await _db.SaveChangesAsync(cancellationToken);

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
    public async Task<IActionResult> Join(IndexViewModel model, CancellationToken cancellationToken)
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
            .SingleOrDefaultAsync(e => e.Code == model.EventCode, cancellationToken);

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
    public async Task<IActionResult> SignIn(string code, CancellationToken cancellationToken)
    {
        var viewModel = await BuildSignInViewModelAsync(code, cancellationToken);
        return viewModel is null ? NotFound() : View(viewModel);
    }

    /// <summary>
    /// Handles the sign-in form submission for an event, validating and processing the provided display name and color.
    /// </summary>
    [HttpPost("/event/{code}/signin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(string code, EventSignInViewModel model, CancellationToken cancellationToken)
    {
        // Check if the event code in the URL is valid and corresponds to an existing event.
        // If not, return a 404 Not Found response.
        var viewModel = await BuildSignInViewModelAsync(code, cancellationToken);
        if (viewModel is null)
        {
            return NotFound();
        }

        // Normalize user input now, but do not persist anything yet.
        model.DisplayName = model.DisplayName?.Trim() ?? string.Empty;
        model.Color = model.Color?.Trim() ?? "ff66c4";

        // Repopulate the page model so the dropdown and event data stay intact.
        viewModel.DisplayName = model.DisplayName;
        viewModel.Color = model.Color;
        viewModel.SelectedExistingDisplayName = model.SelectedExistingDisplayName;

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        // Display a success message to the user and redisplay the form.
        // This is a placeholder for the sign-in logic that will be implemented in the future.
        ViewData["SuccessMessage"] = "Success!";
        return View(viewModel);
    }

    /// <summary>
    /// Builds an event sign-in view model for the specified event code, including participant and display name
    /// information.
    /// </summary>
    /// <remarks>If the specified event code does not correspond to an existing event, or if the code is null
    /// or whitespace, the method returns null. The returned view model includes a list of existing participant display
    /// names and pre-populates fields for the current user if available.</remarks>
    private async Task<EventSignInViewModel?> BuildSignInViewModelAsync(string code, CancellationToken cancellationToken)
    {
        // Validate the event code input to ensure it's not null, empty, or whitespace.
        // If it is, return null to indicate that the sign-in page cannot be built.
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        // Look up the event in the database using the provided code, ensuring that we do not track the entity
        // since we only need read access.
        var eventEntity = await _db.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Code == code, cancellationToken);

        if (eventEntity is null)
        {
            return null;
        }

        // Retrieve a list of existing participant display names for the event, ordered alphabetically.
        var existingDisplayNames = await _db.Participants
            .AsNoTracking()
            .Where(p => p.EventId == eventEntity.Id)
            .Select(p => p.DisplayName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

        var user = await _userManager.GetUserAsync(User);

        // Set default values for the display name and color.
        var displayName = string.Empty;
        var color = "ff66c4";
        string? selectedDisplayName = null;

        // If the user is logged in, attempt to pre-populate the display name and color based on their existing
        // participant record for this event, if it exists.
        if (user is not null)
        {
            // Look for an existing participant record for the current user and event.
            var existingParticipant = await _db.Participants
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.EventId == eventEntity.Id && p.UserId == user.Id,
                    cancellationToken);

            // If a participant record exists, use its display name and color to pre-populate the form.
            // Also set the selected existing display name to match the participant's current display name.
            if (existingParticipant is not null)
            {
                displayName = existingParticipant.DisplayName;
                color = existingParticipant.Color;
                selectedDisplayName = existingParticipant.DisplayName;
            }
            // If no participant record exists for the user and event, attempt to use the user's profile information
            else
            {
                // If the user's display name is not null or whitespace, trim it and use it as the default display name,
                if (!string.IsNullOrWhiteSpace(user.DisplayName))
                {
                    var trimmedDisplayName = user.DisplayName.Trim();
                    if (trimmedDisplayName.Length <= ModelConstants.ParticipantDisplayNameMaxLength)
                    {
                        displayName = trimmedDisplayName;
                    }
                }

                // If the user's color is not null or whitespace, trim it and use it as the default color.
                if (!string.IsNullOrWhiteSpace(user.Color))
                {
                    color = user.Color.Trim();
                }
            }
        }

        // Construct and return the EventSignInViewModel with all the necessary data for rendering the sign-in page.
        return new EventSignInViewModel
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
    }
}
