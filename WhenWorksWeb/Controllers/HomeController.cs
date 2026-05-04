using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WhenWorksWeb.Data;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers
{
	/// <summary>
	/// Represents the controller responsible for handling requests to the application's home pages, including the default,
	/// privacy, and error views.
	/// </summary>
	/// <remarks>The HomeController provides actions for rendering the main entry points of the application. It
	/// includes standard actions for the home and privacy pages, as well as an error action that displays user-friendly
	/// error information when an unhandled exception occurs.</remarks>
	public class HomeController : Controller
	{
        private readonly ApplicationDbContext _db;

        public HomeController(ApplicationDbContext db)
        {
            _db = db;
        }

		/// <summary>
		/// Returns the default view for the Index (Home) page.
		/// </summary>
		[HttpGet]
		public IActionResult Index()
		{
			return View();
		}

		/// <summary>
		/// Returns the Privacy view.
		/// </summary>
		public IActionResult Privacy()
		{
			return View();
		}

        /// <summary>
        /// Processes a request to join an event using the provided event code from the view model.
        /// </summary>
        /// <remarks>If the event code does not match any existing event, a model error is added and the
        /// user remains on the join view. The method requires a valid anti-forgery token and is intended to be called
        /// via HTTP POST.</remarks>
        /// <param name="model">The view model containing the event code entered by the user. Must not be null and should contain a valid
        /// event code.</param>
        /// <returns
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinEvent(IndexViewModel model)
        {
            // Validate the model state to ensure that the event code is provided and meets any necessary validation criteria.
            // If the model state is invalid, return the user to the Index view with the current model to display validation errors.
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            // Trim the event code to remove any leading or trailing whitespace before querying the database.
            var code = model.EventCode.Trim();

            // Query the database for an event that matches the provided code.
            // Use AsNoTracking for better performance since we do not intend to modify the entity.
            var eventEntity = await _db.Events
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.Code == code);

            // If no event is found with the provided code, add a model error to inform the user and return to the Index view.
            if (eventEntity is null)
            {
                ModelState.AddModelError(nameof(IndexViewModel.EventCode), "No event was found for that code.");
                return View("Index", model);
            }

            // Redirect to your event page here.
            ViewData["SuccessMessage"] = "Success! Event code was found.";
            return View("Index", model);
        }

        /// <summary>
        /// Returns the error view with details about the current request for display to the user.
        /// </summary>
        /// <remarks>This action is typically used to display a user-friendly error page when an unhandled exception
        /// occurs. The response is not cached to ensure that error details are not stored or reused.</remarks>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}
	}
}
