using Microsoft.AspNetCore.Mvc;
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
    // Service responsible for generating unique event codes.
    private readonly EventCodeGenerator _codeGenerator;

    public EventsController(ApplicationDbContext db, EventCodeGenerator codeGenerator)
    {
        _db = db;
        _codeGenerator = codeGenerator;
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
        // Normalize the event name by trimming whitespace.
        model.CreateEventName = model.CreateEventName.Trim();

        // Validate that the event name is not empty and that the model state is valid.
        if (string.IsNullOrWhiteSpace(model.CreateEventName) || !ModelState.IsValid)
        {
            // If validation fails, redisplay the form with the current model to show validation errors.
            return View("~/Views/Home/Index.cshtml", model);
        }

        // Generate a unique event code using the EventCodeGenerator service.
        var code = await _codeGenerator.GenerateUniqueCodeAsync();

        // Create a new Event entity with the generated code and the provided event name.
        var eventEntity = new Event
        {
            Code = code,
            Title = model.CreateEventName
        };

        // Add the new event to the database context and save changes to persist it in the database.
        _db.Events.Add(eventEntity);
        await _db.SaveChangesAsync();

        // Redirect the user to the sign-in page for the newly created event using its unique code.
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
        // Validate that the event code is not empty and that the model state is valid.
        if (!ModelState.IsValid)
        {
            // If validation fails, redisplay the form with the current model to show validation errors.
            return View("~/Views/Home/Index.cshtml", model);
        }

        // Normalize the event code by trimming whitespace and converting to uppercase for consistent lookup.
        var code = model.EventCode.Trim().ToUpperInvariant();

        // Attempt to find an event in the database that matches the provided code, using AsNoTracking for read-only access.
        var eventEntity = await _db.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Code == code);

        // If no event is found with the provided code, add a model error and redisplay the form.
        if (eventEntity is null)
        {
            ModelState.AddModelError(nameof(IndexViewModel.EventCode), "No event was found for that code.");
            return View("~/Views/Home/Index.cshtml", model);
        }

        // Redirect the user to the sign-in page for the found event using its unique code.
        return RedirectToRoute("EventSignIn", new { code = eventEntity.Code });
    }

    /// <summary>
    /// Displays the sign-in page for the event associated with the specified event code.
    /// </summary>
    [HttpGet("/event/{code}/signin", Name = "EventSignIn")]
    public IActionResult SignIn(string code)
    {
        // Validate that the event code is not null, empty, or whitespace. If it is, return a 404 Not Found response.
        if (string.IsNullOrWhiteSpace(code))
        {
            return NotFound();
        }

        // Create a view model for the event sign-in page, normalizing the event code to uppercase for display.
        var viewModel = new EventSignInViewModel
        {
            Code = code.ToUpperInvariant()
        };

        // Return the view for the event sign-in page, passing the view model to it.
        return View(viewModel);
    }
}
