using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Areas.Admin;

/// <summary>
/// Enforces that any signed-in Admin-role account has two-factor authentication enabled before it
/// can use an <c>Areas/Admin</c> page. Applied to every page in the area via a folder convention
/// registered in <c>Program.cs</c> (see <see cref="RequireTwoFactorPageFilterFactory"/>) rather
/// than being wired into each page individually, so any future Admin page is covered
/// automatically. This deliberately lives outside <c>Areas/Identity</c> -- see
/// Spec/Features/FEATURES-two-factor-authentication.ospec for why granting the Admin role no
/// longer itself requires 2FA to already be enabled; instead this filter redirects an
/// unprotected Admin account to set it up.
/// </summary>
/// <param name="userManager">Resolves the current user's role membership and 2FA status.</param>
public class RequireTwoFactorPageFilter(UserManager<ApplicationUser> userManager) : IAsyncPageFilter
{
    /// <summary>No handler-selection logic is needed -- the check runs at execution time instead.</summary>
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    /// <summary>
    /// Short-circuits the request with a redirect to <c>EnableAuthenticator</c> when the current
    /// user is an Admin without 2FA enabled; otherwise lets the page handler run normally.
    /// </summary>
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var principal = context.HttpContext.User;

        // [Authorize(Roles = "Admin")] on the page itself already rejects anyone who isn't
        // authenticated and in the Admin role before this filter runs, but check defensively
        // rather than assuming that ordering -- a future page in this area might not carry it.
        if (principal.Identity?.IsAuthenticated == true && principal.IsInRole("Admin"))
        {
            var user = await userManager.GetUserAsync(principal);
            if (user != null && !await userManager.GetTwoFactorEnabledAsync(user))
            {
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectResult($"/Identity/Account/Manage/EnableAuthenticator?returnUrl={Uri.EscapeDataString(returnUrl)}");
                return;
            }
        }

        await next();
    }
}

/// <summary>
/// Wraps <see cref="RequireTwoFactorPageFilter"/> so it can be registered as a plain object in a
/// Razor Pages folder convention (<c>Program.cs</c>) while still resolving its
/// <see cref="UserManager{TUser}"/> dependency from DI per request, the same way a page/controller
/// constructor would.
/// </summary>
public class RequireTwoFactorPageFilterFactory : IFilterFactory
{
    /// <summary>Not reusable -- a fresh instance is created per request so its resolved <see cref="UserManager{TUser}"/> is scoped correctly.</summary>
    public bool IsReusable => false;

    /// <summary>Resolves a new <see cref="RequireTwoFactorPageFilter"/> from the current request's DI container.</summary>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return new RequireTwoFactorPageFilter(userManager);
    }
}
