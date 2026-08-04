using Microsoft.AspNetCore.Mvc;
using WhenWorksWeb.Controllers;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;

namespace WhenWorksWeb.Tests.Controllers;

/// <summary>
/// Tier 2 tests for <see cref="HomeController"/>. No database is involved — these actions are pure view
/// dispatch — so this doesn't inherit <see cref="SqliteDbContextFixture"/>.
/// </summary>
public class HomeControllerTests
{
    /// <summary>
    /// Builds a <see cref="HomeController"/> with a real, minimal <see cref="ControllerContext"/> attached.
    /// </summary>
    private static HomeController CreateController()
    {
        var controller = new HomeController();
        ControllerTestContext.AttachContext(controller);
        return controller;
    }

    /// <summary>
    /// Index should render the default view with an empty view model ready for the create/join form.
    /// </summary>
    [Fact]
    public void Index_ReturnsViewWithIndexViewModel()
    {
        var controller = CreateController();

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.IsType<IndexViewModel>(viewResult.Model);
    }

    /// <summary>
    /// Privacy should render its view with no model.
    /// </summary>
    [Fact]
    public void Privacy_ReturnsView()
    {
        var controller = CreateController();

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }

    /// <summary>
    /// Error should render the error view populated with the current request's trace identifier and standard
    /// title/button text, since Activity.Current is null outside a real request pipeline.
    /// </summary>
    [Fact]
    public void Error_ReturnsViewWithErrorViewModel_UsingHttpContextTraceIdentifier()
    {
        var controller = CreateController();
        controller.HttpContext.TraceIdentifier = "trace-123";

        var result = controller.Error();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal("trace-123", model.RequestId);
        Assert.Equal("Error", model.Title);
        Assert.Equal("Return Home", model.ReturnButtonText);
    }
}
