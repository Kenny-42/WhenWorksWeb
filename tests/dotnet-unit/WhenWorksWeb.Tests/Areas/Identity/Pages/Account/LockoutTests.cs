using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Areas.Identity.Pages.Account;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 3 tests for <see cref="LockoutModel.OnGetAsync"/>'s <see cref="LockoutModel.TimeRemaining"/>
/// computation, driven directly against the page model (via <see cref="PageModelTestContext"/>) rather
/// than through Login.cshtml.cs's redirect -- the behavior under test is this page's own lookup and
/// rounding logic, not how it's reached. <see cref="LoginTests"/> covers the redirect handoff end to
/// end (Login.cshtml.cs setting <c>LockedOutUserName</c>, this page reading it back and rendering a
/// countdown). See Spec/Features/FEATURES-enforce-account-lockout.ospec.
/// </summary>
public class LockoutTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Str0ng!Pass";

    private readonly CustomWebApplicationFactory _factory;

    public LockoutTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private (LockoutModel Model, IServiceScope Scope) CreateModel()
    {
        var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var model = new LockoutModel(userManager);
        PageModelTestContext.AttachContext(model, requestServices: scope.ServiceProvider);

        return (model, scope);
    }

    private async Task<ApplicationUser> CreateUserAsync(IServiceScope scope, string userName, DateTimeOffset? lockoutEnd = null)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        user.EmailConfirmed = true;

        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        if (lockoutEnd is { } end)
        {
            var lockoutResult = await userManager.SetLockoutEndDateAsync(user, end);
            Assert.True(lockoutResult.Succeeded, string.Join("; ", lockoutResult.Errors.Select(e => e.Description)));
        }

        return user;
    }

    /// <summary>
    /// The normal case: a currently-locked-out account's remaining time, rounded up to the next whole
    /// minute (4 minutes 10 seconds left still reads as "5 minutes remaining", not "4").
    /// </summary>
    [Fact]
    public async Task OnGetAsync_WithLockedOutAccount_SetsTimeRemainingRoundedUpToNextMinute()
    {
        var (model, scope) = CreateModel();
        using (scope)
        {
            await CreateUserAsync(scope, "lockoutpagetimeremaininguser", DateTimeOffset.UtcNow.AddMinutes(4).AddSeconds(10));
            model.LockedOutUserName = "lockoutpagetimeremaininguser";

            await model.OnGetAsync();

            Assert.NotNull(model.TimeRemaining);
            Assert.Equal(TimeSpan.FromMinutes(5), model.TimeRemaining!.Value);
        }
    }

    /// <summary>
    /// No TempData was handed off -- e.g. the page was requested directly rather than via a
    /// Login.cshtml.cs redirect. Must not throw or attempt a lookup with an empty username.
    /// </summary>
    [Fact]
    public async Task OnGetAsync_WithNoLockedOutUserName_LeavesTimeRemainingNull()
    {
        var (model, scope) = CreateModel();
        using (scope)
        {
            model.LockedOutUserName = null;

            await model.OnGetAsync();

            Assert.Null(model.TimeRemaining);
        }
    }

    /// <summary>
    /// The account named in TempData no longer exists (e.g. deleted between the redirect and this
    /// request) -- falls back to no countdown rather than throwing on a null user.
    /// </summary>
    [Fact]
    public async Task OnGetAsync_WithUnknownUserName_LeavesTimeRemainingNull()
    {
        var (model, scope) = CreateModel();
        using (scope)
        {
            model.LockedOutUserName = "no-such-user";

            await model.OnGetAsync();

            Assert.Null(model.TimeRemaining);
        }
    }

    /// <summary>
    /// A narrow timing race: the account's lockout expired between Login.cshtml.cs's redirect and
    /// this request. Must not display a negative or zero countdown.
    /// </summary>
    [Fact]
    public async Task OnGetAsync_WithExpiredLockout_LeavesTimeRemainingNull()
    {
        var (model, scope) = CreateModel();
        using (scope)
        {
            await CreateUserAsync(scope, "expiredlockoutuser", DateTimeOffset.UtcNow.AddMinutes(-1));
            model.LockedOutUserName = "expiredlockoutuser";

            await model.OnGetAsync();

            Assert.Null(model.TimeRemaining);
        }
    }

    /// <summary>
    /// The account named in TempData exists but was never locked out at all -- e.g. reached this page
    /// some other way. Must not display a countdown built from a null <c>LockoutEnd</c>.
    /// </summary>
    [Fact]
    public async Task OnGetAsync_WithAccountNeverLockedOut_LeavesTimeRemainingNull()
    {
        var (model, scope) = CreateModel();
        using (scope)
        {
            await CreateUserAsync(scope, "neverlockedoutuser");
            model.LockedOutUserName = "neverlockedoutuser";

            await model.OnGetAsync();

            Assert.Null(model.TimeRemaining);
        }
    }
}
