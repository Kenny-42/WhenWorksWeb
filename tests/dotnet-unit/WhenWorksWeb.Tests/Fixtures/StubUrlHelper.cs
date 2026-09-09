using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Minimal <see cref="IUrlHelper"/> that returns a deterministic, inspectable URL instead of
/// performing real route resolution. Building a fully route-aware helper requires a real endpoint
/// pipeline (that's what the WebApplicationFactory-based Tier 3 tests are for) -- URL generation is
/// framework plumbing here, not the business logic these direct PageModel/controller tests check.
/// </summary>
public sealed class StubUrlHelper(ActionContext actionContext) : IUrlHelper
{
    public ActionContext ActionContext { get; } = actionContext;

    public string? Action(UrlActionContext urlActionContext) => $"/stub-action/{urlActionContext.Action}/{urlActionContext.Controller}";

    public string? Content(string? contentPath) => contentPath;

    public bool IsLocalUrl(string? url) => true;

    public string? Link(string? routeName, object? values) => $"/stub-link/{routeName}";

    public string? RouteUrl(UrlRouteContext routeContext) => $"/stub-route/{routeContext.RouteName}";
}
