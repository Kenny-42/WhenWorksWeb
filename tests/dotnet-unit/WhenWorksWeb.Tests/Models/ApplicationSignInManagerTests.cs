using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 3 tests exercising <see cref="ApplicationSignInManager.CheckPasswordSignInAsync"/> directly
/// against a real, DI-resolved instance -- the seam <c>LoginTests</c> exercises indirectly through
/// the whole Login page. Direct coverage here pins the override's own contract (password checked
/// before status, hashed exactly once, status ordering matching the base <c>PreSignInCheck</c>)
/// independent of Login.cshtml.cs's branching, per
/// Spec/Refactors/REFACTOR-signin-manager-and-confirmation-email.ospec.
/// </summary>
public class ApplicationSignInManagerTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Str0ng!Pass";

    private readonly CustomWebApplicationFactory _factory;

    public ApplicationSignInManagerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // Ensures the app (and its DI container/routing) is actually built before a test resolves
        // services from a bare CreateScope() below, matching ExternalLoginTests' pattern.
        _ = factory.Server;
    }

    private async Task<(ApplicationSignInManager SignInManager, ApplicationUser User, IServiceScope Scope)> CreateUserAndSignInManagerAsync(
        string userName, bool emailConfirmed, bool lockedOut = false, bool twoFactorEnabled = false)
    {
        var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // The registered SignInManager<ApplicationUser> is ApplicationSignInManager itself (see
        // Program.cs's AddSignInManager<ApplicationSignInManager>()) -- resolving the base type is
        // enough to get the override, and matches how every real caller (e.g. Login.cshtml.cs)
        // receives it.
        var signInManager = (ApplicationSignInManager)scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        signInManager.Context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        user.EmailConfirmed = emailConfirmed;
        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        if (lockedOut)
        {
            var lockoutResult = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(30));
            Assert.True(lockoutResult.Succeeded, string.Join("; ", lockoutResult.Errors.Select(e => e.Description)));
        }

        if (twoFactorEnabled)
        {
            var twoFactorResult = await userManager.SetTwoFactorEnabledAsync(user, true);
            Assert.True(twoFactorResult.Succeeded, string.Join("; ", twoFactorResult.Errors.Select(e => e.Description)));
        }

        return (signInManager, user, scope);
    }

    [Fact]
    public async Task CheckPasswordSignInAsync_WithWrongPassword_ReturnsFailed()
    {
        var (signInManager, user, scope) = await CreateUserAndSignInManagerAsync("wrongpassworduser", emailConfirmed: true);
        using (scope)
        {
            var result = await signInManager.CheckPasswordSignInAsync(user, "definitely-the-wrong-password", lockoutOnFailure: false);

            Assert.Same(SignInResult.Failed, result);
        }
    }

    /// <summary>
    /// The oracle this override closes: a wrong password against an unconfirmed account must return
    /// the same <see cref="SignInResult.Failed"/> as any other wrong password, never
    /// <see cref="SignInResult.NotAllowed"/> -- unlike the base class's own ordering, which checks
    /// confirmation before the password.
    /// </summary>
    [Fact]
    public async Task CheckPasswordSignInAsync_WithWrongPasswordAndUnconfirmedAccount_ReturnsFailedNotNotAllowed()
    {
        var (signInManager, user, scope) = await CreateUserAndSignInManagerAsync("wrongpasswordunconfirmeduser", emailConfirmed: false);
        using (scope)
        {
            var result = await signInManager.CheckPasswordSignInAsync(user, "definitely-the-wrong-password", lockoutOnFailure: false);

            Assert.Same(SignInResult.Failed, result);
            Assert.False(result.IsNotAllowed);
        }
    }

    /// <summary>
    /// Same oracle, for lockout: a wrong password against a locked-out account must return
    /// <see cref="SignInResult.Failed"/>, never <see cref="SignInResult.LockedOut"/>.
    /// </summary>
    [Fact]
    public async Task CheckPasswordSignInAsync_WithWrongPasswordAndLockedOutAccount_ReturnsFailedNotLockedOut()
    {
        var (signInManager, user, scope) = await CreateUserAndSignInManagerAsync("wrongpasswordlockedoutuser", emailConfirmed: true, lockedOut: true);
        using (scope)
        {
            var result = await signInManager.CheckPasswordSignInAsync(user, "definitely-the-wrong-password", lockoutOnFailure: false);

            Assert.Same(SignInResult.Failed, result);
            Assert.False(result.IsLockedOut);
        }
    }

    [Fact]
    public async Task CheckPasswordSignInAsync_WithCorrectPasswordAndConfirmedAccount_ReturnsSuccess()
    {
        var (signInManager, user, scope) = await CreateUserAndSignInManagerAsync("correctpasworduser", emailConfirmed: true);
        using (scope)
        {
            var result = await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: false);

            Assert.True(result.Succeeded);
        }
    }

    [Fact]
    public async Task CheckPasswordSignInAsync_WithCorrectPasswordAndUnconfirmedAccount_ReturnsNotAllowed()
    {
        var (signInManager, user, scope) = await CreateUserAndSignInManagerAsync("correctpasswordunconfirmeduser", emailConfirmed: false);
        using (scope)
        {
            var result = await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: false);

            Assert.True(result.IsNotAllowed);
        }
    }

    [Fact]
    public async Task CheckPasswordSignInAsync_WithCorrectPasswordAndLockedOutAccount_ReturnsLockedOut()
    {
        var (signInManager, user, scope) = await CreateUserAndSignInManagerAsync("correctpasswordlockedoutuser", emailConfirmed: true, lockedOut: true);
        using (scope)
        {
            var result = await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: false);

            Assert.True(result.IsLockedOut);
        }
    }

    /// <summary>
    /// The lockout-timing bug this guards against: a correct password against a 2FA-enabled account
    /// that isn't remembered on this device must NOT reset the failed-access counter yet -- sign-in
    /// isn't actually complete until the second factor is verified (mirroring the base
    /// <c>SignInManager</c>'s own <c>CheckPasswordSignInCoreAsync</c> guard). Resetting here would let
    /// an attacker who has the password (but not the second factor) launder away a partial lockout
    /// count by repeatedly stopping at this stage.
    /// </summary>
    [Fact]
    public async Task CheckPasswordSignInAsync_WithCorrectPasswordAndUnrememberedTwoFactorAccount_DoesNotResetLockoutCounter()
    {
        var (signInManager, user, scope) = await CreateUserAndSignInManagerAsync(
            "correctpasswordtwofactoruser", emailConfirmed: true, twoFactorEnabled: true);
        using (scope)
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await userManager.AccessFailedAsync(user);
            var failedCountBeforeCheck = await userManager.GetAccessFailedCountAsync(user);
            Assert.Equal(1, failedCountBeforeCheck);

            var result = await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: false);

            // CheckPasswordSignInAsync itself still reports Success here -- the base SignInManager's
            // higher-level PasswordSignInAsync is what turns this into RequiresTwoFactor by consulting
            // 2FA state separately. What this test pins is the lockout-counter side effect below.
            Assert.True(result.Succeeded);
            var failedCountAfterCheck = await userManager.GetAccessFailedCountAsync(user);
            Assert.Equal(1, failedCountAfterCheck);
        }
    }

    /// <summary>
    /// Contrast case: an account with 2FA disabled still gets its lockout counter reset immediately
    /// on a correct password, exactly as before this fix -- only the 2FA-enabled, not-yet-remembered
    /// case defers the reset.
    /// </summary>
    [Fact]
    public async Task CheckPasswordSignInAsync_WithCorrectPasswordAndTwoFactorDisabled_StillResetsLockoutCounter()
    {
        var (signInManager, user, scope) = await CreateUserAndSignInManagerAsync(
            "correctpasswordnotwofactoruser", emailConfirmed: true, twoFactorEnabled: false);
        using (scope)
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await userManager.AccessFailedAsync(user);
            Assert.Equal(1, await userManager.GetAccessFailedCountAsync(user));

            var result = await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: false);

            Assert.True(result.Succeeded);
            Assert.Equal(0, await userManager.GetAccessFailedCountAsync(user));
        }
    }

    /// <summary>
    /// The branch Login.cshtml.cs now actually exercises (see
    /// Spec/Features/FEATURES-enforce-account-lockout.ospec): with <c>lockoutOnFailure: true</c>, a
    /// single wrong password increments <c>AccessFailedCount</c> by one and, since that's below
    /// <c>IdentityConfiguration.Configure</c>'s 5-attempt threshold, still returns
    /// <see cref="SignInResult.Failed"/> rather than locking the account out.
    /// </summary>
    [Fact]
    public async Task CheckPasswordSignInAsync_WithWrongPasswordAndLockoutOnFailureTrue_IncrementsAccessFailedCountBelowThreshold()
    {
        var (signInManager, user, scope) = await CreateUserAndSignInManagerAsync("lockoutonfailuretrueuser", emailConfirmed: true);
        using (scope)
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var result = await signInManager.CheckPasswordSignInAsync(user, "definitely-the-wrong-password", lockoutOnFailure: true);

            Assert.Same(SignInResult.Failed, result);
            Assert.False(result.IsLockedOut);
            Assert.Equal(1, await userManager.GetAccessFailedCountAsync(user));
        }
    }

    /// <summary>
    /// The threshold crossing: the 5th wrong password (4 already recorded, matching
    /// <c>IdentityOptions.Lockout.MaxFailedAccessAttempts</c>) locks the account out and the same
    /// call reports it, rather than requiring a separate lookup.
    /// </summary>
    [Fact]
    public async Task CheckPasswordSignInAsync_WithWrongPasswordReachingMaxFailedAccessAttempts_LocksOutAccount()
    {
        var (signInManager, user, scope) = await CreateUserAndSignInManagerAsync("lockoutthresholduser", emailConfirmed: true);
        using (scope)
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var maxAttempts = userManager.Options.Lockout.MaxFailedAccessAttempts;
            for (var i = 0; i < maxAttempts - 1; i++)
            {
                await userManager.AccessFailedAsync(user);
            }
            Assert.Equal(maxAttempts - 1, await userManager.GetAccessFailedCountAsync(user));
            Assert.False(await userManager.IsLockedOutAsync(user));

            var result = await signInManager.CheckPasswordSignInAsync(user, "definitely-the-wrong-password", lockoutOnFailure: true);

            Assert.True(result.IsLockedOut);
            Assert.True(await userManager.IsLockedOutAsync(user));
        }
    }

    /// <summary>
    /// A correct password with <c>lockoutOnFailure: true</c> still resets the counter accumulated by
    /// prior wrong attempts, exactly as the <c>lockoutOnFailure: false</c> case above -- the reset
    /// path doesn't depend on the parameter that controls whether failures are counted.
    /// </summary>
    [Fact]
    public async Task CheckPasswordSignInAsync_WithCorrectPasswordAfterFailedAttemptsAndLockoutOnFailureTrue_ResetsAccessFailedCount()
    {
        var (signInManager, user, scope) = await CreateUserAndSignInManagerAsync("lockoutresetuser", emailConfirmed: true);
        using (scope)
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await userManager.AccessFailedAsync(user);
            await userManager.AccessFailedAsync(user);
            Assert.Equal(2, await userManager.GetAccessFailedCountAsync(user));

            var result = await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);

            Assert.True(result.Succeeded);
            Assert.Equal(0, await userManager.GetAccessFailedCountAsync(user));
        }
    }
}
