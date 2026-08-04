using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Helpers for wiring a controller instance with the minimum real <see cref="HttpContext"/>/<see cref="ControllerContext"/>
/// state its actions need (current user, request cookies, URL generation), without a full HTTP pipeline.
/// </summary>
public static class ControllerTestContext
{
    /// <summary>
    /// Attaches a fresh <see cref="ControllerContext"/> to <paramref name="controller"/>, with the given
    /// <paramref name="user"/> (or anonymous, if null) and <paramref name="requestCookies"/> pre-populated on
    /// the request. Returns the underlying <see cref="DefaultHttpContext"/> so a test can inspect
    /// <c>Response.Headers["Set-Cookie"]</c> afterward.
    /// </summary>
    public static DefaultHttpContext AttachContext(
        Controller controller,
        ApplicationUser? user = null,
        IReadOnlyDictionary<string, string>? requestCookies = null)
    {
        var httpContext = new DefaultHttpContext
        {
            // ControllerBase.TryValidateModel and Controller.View/TempData resolve services (IObjectModelValidator,
            // ITempDataDictionaryFactory, etc.) from RequestServices — a bare DefaultHttpContext leaves this
            // null. AddControllersWithViews mirrors exactly what Program.cs registers in production.
            RequestServices = new ServiceCollection().AddLogging().AddControllersWithViews().Services.BuildServiceProvider()
        };
        httpContext.User = user is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user.Id)],
                authenticationType: "Test"));

        if (requestCookies is { Count: > 0 })
        {
            httpContext.Request.Headers.Append(
                "Cookie",
                string.Join("; ", requestCookies.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        controller.ControllerContext = new ControllerContext(actionContext);

        // Url.RouteUrl/Url.Action need an IUrlHelper. Building a fully route-aware one requires a real endpoint
        // pipeline (that's what the WebApplicationFactory-based Tier 3 tests are for) — this stub returns a
        // predictable, inspectable value instead, since URL generation is framework plumbing, not the business
        // logic these tests are checking.
        controller.Url = new StubUrlHelper(actionContext);

        return httpContext;
    }

    /// <summary>
    /// Reads a cookie value the controller set on the response via <c>Response.Cookies.Append</c>, by parsing
    /// the real <c>Set-Cookie</c> header — not a mock of the cookie APIs, just inspecting their real output.
    /// </summary>
    public static string? GetResponseCookieValue(HttpContext httpContext, string cookieName)
    {
        foreach (var setCookieHeader in httpContext.Response.Headers.SetCookie)
        {
            if (setCookieHeader is null)
            {
                continue;
            }

            var firstSegment = setCookieHeader.Split(';', 2)[0];
            var parts = firstSegment.Split('=', 2);
            if (parts.Length == 2 && parts[0] == cookieName)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }

    /// <summary>
    /// Minimal <see cref="IUrlHelper"/> that returns a deterministic, inspectable URL instead of performing
    /// real route resolution (see the remark on <see cref="AttachContext"/> for why).
    /// </summary>
    private sealed class StubUrlHelper(ActionContext actionContext) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = actionContext;

        public string? Action(UrlActionContext urlActionContext) => $"/stub-action/{urlActionContext.Action}/{urlActionContext.Controller}";

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url) => true;

        public string? Link(string? routeName, object? values) => $"/stub-link/{routeName}";

        public string? RouteUrl(UrlRouteContext routeContext) => $"/stub-route/{routeContext.RouteName}";
    }
}
