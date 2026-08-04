using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Security;

/// <summary>
/// Tier 3 tests for the <c>[Authorize(Roles = "Admin")]</c> boundary on <c>Areas/Admin</c> pages, via the real
/// authorization pipeline (see <see cref="TestAuthHandler"/> for how a request becomes authenticated as a
/// specific user/role set without going through Identity's real login UI).
/// </summary>
public class AdminAuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminAuthorizationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    private async Task<ApplicationUser> CreateUserAsync(string userName, bool asAdmin = false)
    {
        // Create + role-assign in one scope/DbContext instance — attaching the same entity object into a
        // second, separate scope's change tracker for the role update risks an identity-map conflict if
        // anything else in that later scope has already touched an entity with the same key.
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        if (asAdmin)
        {
            var roleResult = await userManager.AddToRoleAsync(user, "Admin");
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }

        return user;
    }

    /// <summary>
    /// An anonymous request must be challenged (redirected to the login page), not served the page or met
    /// with a generic error.
    /// </summary>
    [Fact]
    public async Task ManageUsers_WhenAnonymous_RedirectsToLogin()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/Admin/Users/ManageUsers");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An authenticated user who is not in the Admin role must be rejected (redirected to the access-denied
    /// page), not silently granted access just for being logged in.
    /// </summary>
    [Fact]
    public async Task ManageUsers_WhenAuthenticatedWithoutAdminRole_RedirectsToAccessDenied()
    {
        var user = await CreateUserAsync("nonadmin");
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id);

        var response = await client.GetAsync("/Admin/Users/ManageUsers");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/AccessDenied", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An authenticated user who genuinely holds the Admin role must be let through — proves the boundary
    /// actually opens for the right principal, not just that it closes for the wrong ones.
    /// </summary>
    [Fact]
    public async Task ManageUsers_WhenAuthenticatedWithAdminRole_ReturnsOk()
    {
        var admin = await CreateUserAsync("realadmin", asAdmin: true);

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", admin.Id);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var response = await client.GetAsync("/Admin/Users/ManageUsers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Holding some other role must not be enough — the boundary is specifically the Admin role, not "any
    /// authenticated role."
    /// </summary>
    [Fact]
    public async Task ManageUsers_WhenAuthenticatedWithDifferentRole_RedirectsToAccessDenied()
    {
        var user = await CreateUserAsync("regularuser");

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        var response = await client.GetAsync("/Admin/Users/ManageUsers");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/AccessDenied", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
