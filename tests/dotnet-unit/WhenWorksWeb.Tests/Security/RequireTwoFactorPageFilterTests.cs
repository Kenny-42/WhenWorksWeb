using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Security;

/// <summary>
/// Tier 3 tests for <see cref="WhenWorksWeb.Areas.Admin.RequireTwoFactorPageFilter"/> -- the
/// Admin-role-requires-2FA enforcement point added by
/// Spec/Features/FEATURES-two-factor-authentication.ospec. Uses the same
/// <see cref="CustomWebApplicationFactory"/>/<see cref="TestAuthHandler"/> setup as
/// <see cref="AdminAuthorizationTests"/> to become authenticated as a given user/role set without
/// going through Identity's real login UI.
/// </summary>
public class RequireTwoFactorPageFilterTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RequireTwoFactorPageFilterTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    private async Task<ApplicationUser> CreateAdminAsync(string userName, bool twoFactorEnabled)
    {
        // Create + role-assign + 2FA flag in one scope/DbContext instance, same reasoning as
        // AdminAuthorizationTests.CreateUserAsync -- attaching the same entity into a second,
        // separate scope's change tracker risks an identity-map conflict.
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        var roleResult = await userManager.AddToRoleAsync(user, "Admin");
        Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(e => e.Description)));

        if (twoFactorEnabled)
        {
            await userManager.SetTwoFactorEnabledAsync(user, true);
        }

        return user;
    }

    /// <summary>
    /// An Admin-role account without 2FA enabled must be redirected to EnableAuthenticator before
    /// it can use an Admin page, rather than being let straight through.
    /// </summary>
    [Fact]
    public async Task ManageUsers_WhenAdminWithoutTwoFactor_RedirectsToEnableAuthenticator()
    {
        var admin = await CreateAdminAsync("adminno2fa", twoFactorEnabled: false);

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", admin.Id);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var response = await client.GetAsync("/Admin/Users/ManageUsers");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Manage/EnableAuthenticator", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An Admin-role account with 2FA already enabled must be let through normally -- proves the
    /// filter actually opens once the requirement is met, not just that it closes without it.
    /// </summary>
    [Fact]
    public async Task ManageUsers_WhenAdminWithTwoFactor_ReturnsOk()
    {
        var admin = await CreateAdminAsync("adminwith2fa", twoFactorEnabled: true);

        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", admin.Id);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var response = await client.GetAsync("/Admin/Users/ManageUsers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
