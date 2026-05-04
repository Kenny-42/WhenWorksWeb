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
