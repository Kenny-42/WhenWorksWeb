using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Data;
using WhenWorksWeb.Models;
using WhenWorksWeb.Services;

namespace WhenWorksWeb.Controllers;

/// <summary>
/// Provides actions for creating, joining, and signing in to events within the application.
/// </summary>
public partial class EventsController : Controller
{
    /// <summary>
    /// Represents the prefix used for event access cookies in the application.
    /// </summary>
    /// <remarks>This constant is used to identify cookies related to event access. It can be used when
    /// creating, reading, or deleting event access cookies to ensure consistent naming.</remarks>
    private const string EventAccessCookiePrefix = "WhenWorksWeb.EventAccess.";

    private readonly ApplicationDbContext _db;
    private readonly UniqueCodeGenerator _codeGenerator;
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    /// Provides data protection services for event access operations.
    /// </summary>
    /// <remarks>This field is used to secure sensitive event-related data, such as tokens or identifiers, by
    /// encrypting or decrypting information as needed. The specific implementation of IDataProtector determines the
    /// protection algorithm and scope.</remarks>
    private readonly IDataProtector _eventAccessProtector;

    public EventsController(
        ApplicationDbContext db,
        UniqueCodeGenerator codeGenerator,
        UserManager<ApplicationUser> userManager,
        IDataProtectionProvider dataProtectionProvider)
    {
        _db = db;
        _codeGenerator = codeGenerator;
        _userManager = userManager;
        // Create a data protector specifically for event access operations, using a unique purpose string to ensure that the
        // protected data is isolated from other uses of data protection in the application.
        _eventAccessProtector = dataProtectionProvider.CreateProtector("WhenWorksWeb.EventAccess");
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

        var currentUser = await _userManager.GetUserAsync(User);

        // Generate a unique event code using the code generator service.
        var code = await _codeGenerator.GenerateUniqueEventCodeAsync(cancellationToken);
        // Create a new Event entity using the generated code and the provided event name from the model.
        var eventEntity = Event.Create(code, model.CreateEventName!, currentUser?.Id);

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
    /// Returns the event for the provided code or null if it does not exist.
    /// </summary>
    private async Task<Event?> GetEventAsync(string code, CancellationToken cancellationToken)
    {
        // If the code is null, empty, or whitespace, return null immediately without querying the database.
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        // Normalize the code by trimming whitespace and converting it to uppercase to ensure consistent lookup.
        var normalizedCode = code.Trim().ToUpperInvariant();

        // Query the database for an event that matches the normalized code.
        // AsNoTracking is used since we only need read access to check for existence.
        return await _db.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Code == normalizedCode, cancellationToken);
    }

    /// <summary>
    /// Returns a friendly shared error page for an event code that does not exist.
    /// </summary>
    private IActionResult CreateEventNotFoundResult()
    {
        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            Title = "Event Not Found",
            Message = "Sorry, that event does not exist.",
            ReturnUrl = Url.Action("Index", "Home"),
            ReturnButtonText = "Return Home"
        });
    }
}
