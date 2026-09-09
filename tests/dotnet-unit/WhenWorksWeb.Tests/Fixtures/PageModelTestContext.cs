using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Attaches the minimum real Razor Pages plumbing (<see cref="PageContext"/>/<see cref="ModelStateDictionary"/>
/// and a <see cref="ITempDataDictionaryFactory"/> backed by an in-memory, non-persisting
/// <see cref="ITempDataProvider"/>) a <see cref="PageModel"/> needs to run an OnGet/OnPost handler outside a
/// full HTTP pipeline. Mirrors <see cref="ControllerTestContext.AttachContext"/> for MVC controllers, adapted
/// for Razor Pages' <see cref="PageContext"/> instead of <see cref="ControllerContext"/>.
/// </summary>
public static class PageModelTestContext
{
    /// <summary>
    /// Attaches a fresh <see cref="PageContext"/> to <paramref name="pageModel"/>, with the given
    /// <paramref name="user"/> (or anonymous, if null) set as the current principal.
    /// </summary>
    /// <param name="requestServices">
    /// Overrides the minimal, isolated <see cref="IServiceProvider"/> normally built for
    /// <see cref="HttpContext.RequestServices"/>. Pass the real app's request-scoped provider (e.g. from
    /// <c>CustomWebApplicationFactory.Services.CreateScope()</c>) when the handler under test needs more
    /// than logging/TempData -- e.g. <c>SignInManager.SignInAsync</c>'s cookie authentication, which
    /// resolves data protection and the authentication service from <c>RequestServices</c>.
    /// </param>
    public static void AttachContext(PageModel pageModel, ApplicationUser? user = null, IServiceProvider? requestServices = null)
    {
        IServiceProvider resolvedRequestServices;
        if (requestServices is null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ITempDataProvider, NoOpTempDataProvider>();
            services.AddSingleton<ITempDataDictionaryFactory, TempDataDictionaryFactory>();
            resolvedRequestServices = services.BuildServiceProvider();
        }
        else
        {
            resolvedRequestServices = requestServices;
        }

        var httpContext = new DefaultHttpContext
        {
            RequestServices = resolvedRequestServices
        };
        httpContext.User = user is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user.Id)],
                authenticationType: "Test"));

        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        var metadataProvider = new EmptyModelMetadataProvider();
        var viewData = new ViewDataDictionary(metadataProvider, modelState);

        pageModel.PageContext = new PageContext(actionContext) { ViewData = viewData };

        // Url.Page needs an IUrlHelper -- see StubUrlHelper's remarks for why a stub is enough here.
        pageModel.Url = new StubUrlHelper(actionContext);
    }

    /// <summary>
    /// A <see cref="ITempDataProvider"/> that never actually persists anything -- <see cref="PageModel.TempData"/>
    /// still works for in-request reads/writes (backed by <see cref="TempDataDictionary"/>'s own in-memory
    /// store), but nothing is saved to a real backing store (session, cookie) since these tests only exercise a
    /// single simulated request and never need a value to survive past it.
    /// </summary>
    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            // Intentionally no-op -- see class remarks.
        }
    }
}
