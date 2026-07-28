using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Controllers;

/// <summary>
/// Provides actions for handling requests to the application's home pages, including the main landing page, privacy
/// policy, and error display.
/// </summary>
public class HomeController : Controller
{
    /// <summary>
    /// Returns the default view for the Index (Home) page.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View(new IndexViewModel());
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
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            Title = "Error",
            ReturnButtonText = "Return Home"
        });
    }
}
