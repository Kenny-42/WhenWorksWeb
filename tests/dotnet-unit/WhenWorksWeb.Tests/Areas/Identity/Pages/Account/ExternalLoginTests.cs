using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 3 tests proving the fix for the Google sign-in lockout ExternalLogin.cshtml.cs's
/// <c>OnPostConfirmationAsync</c> used to have: creating an account from a first-time external
/// (Google) identity now marks it <see cref="ApplicationUser.EmailConfirmed"/> <see
/// langword="true"/> at creation, since Google already verified the address -- the same thing a
/// clicked confirmation-email link proves for a local registration. Without that, the account's very
/// next Google sign-in was rejected by <see cref="IdentityOptions.SignIn.RequireConfirmedAccount"/>
/// (see Spec/Features/FEATURES-email-verification.ospec) with no way for the user to resolve it
/// (there's no password to reset, and the confirmation form re-submission just failed as a duplicate
/// account).
/// </summary>
/// <remarks>
/// Exercises the real <see cref="SignInManager{TUser}.ExternalLoginSignInAsync(string, string, bool)"/>
/// check directly (the same call <c>OnGetCallbackAsync</c> makes) against a real <see
/// cref="UserManager{TUser}"/>/database, rather than driving the actual Razor Page handlers. Doing
/// that would require faking the OAuth round trip's "Identity.External" cookie (an internal handshake
/// between <c>ConfigureExternalAuthenticationProperties</c> and <c>GetExternalLoginInfoAsync</c>) --
/// reimplementing that framework internal isn't worth it when what actually matters is the interaction
/// between <c>EmailConfirmed</c> and <c>RequireConfirmedAccount</c>, which this exercises for real.
/// </remarks>
public class ExternalLoginTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string LoginProvider = "Google";

    private readonly CustomWebApplicationFactory _factory;

    public ExternalLoginTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates a user already linked to a Google login (mirroring what
    /// <c>OnPostConfirmationAsync</c>'s <c>CreateAsync</c> + <c>AddLoginAsync</c> produce), with
    /// <see cref="ApplicationUser.EmailConfirmed"/> set as the fixed or buggy version of that method
    /// would have left it, and a real, request-scoped <see cref="SignInManager{TUser}"/> to sign in
    /// through.
    /// </summary>
    private async Task<(SignInManager<ApplicationUser> SignInManager, string ProviderKey)> CreateLinkedGoogleUserAsync(
        IServiceProvider scopedServices, string userName, bool emailConfirmed)
    {
        var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scopedServices.GetRequiredService<SignInManager<ApplicationUser>>();

        // SignInManager normally reads this from IHttpContextAccessor.HttpContext, populated by the
        // request pipeline. Outside of one, it must be set explicitly -- RequestServices is the real
        // app container so cookie sign-in's dependencies (data protection, options, etc.) resolve
        // exactly as they do in production.
        signInManager.Context = new DefaultHttpContext { RequestServices = scopedServices };

        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        user.EmailConfirmed = emailConfirmed;
        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        var providerKey = $"google-key-{userName}";
        var loginResult = await userManager.AddLoginAsync(user, new UserLoginInfo(LoginProvider, providerKey, LoginProvider));
        Assert.True(loginResult.Succeeded, string.Join("; ", loginResult.Errors.Select(e => e.Description)));

        return (signInManager, providerKey);
    }

    /// <summary>
    /// The fix under test: an account confirmed at creation (as <c>OnPostConfirmationAsync</c> now
    /// does for every new Google account) can sign in via Google again afterward.
    /// </summary>
    [Fact]
    public async Task ExternalLoginSignInAsync_ForAccountConfirmedAtCreation_Succeeds()
    {
        using var scope = _factory.Services.CreateScope();
        var (signInManager, providerKey) = await CreateLinkedGoogleUserAsync(scope.ServiceProvider, "googleconfirmed", emailConfirmed: true);

        var result = await signInManager.ExternalLoginSignInAsync(LoginProvider, providerKey, isPersistent: false);

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Reproduces the original bug: a Google-linked account that was never marked confirmed is
    /// rejected by <c>RequireConfirmedAccount</c> on its next sign-in. Guards against the fix being
    /// silently reverted -- without <c>SetEmailConfirmedAsync</c> in <c>OnPostConfirmationAsync</c>,
    /// every brand-new Google account would fail this exact check the moment it tried to sign in a
    /// second time.
    /// </summary>
    [Fact]
    public async Task ExternalLoginSignInAsync_ForNeverConfirmedAccount_IsNotAllowed()
    {
        using var scope = _factory.Services.CreateScope();
        var (signInManager, providerKey) = await CreateLinkedGoogleUserAsync(scope.ServiceProvider, "googleunconfirmed", emailConfirmed: false);

        var result = await signInManager.ExternalLoginSignInAsync(LoginProvider, providerKey, isPersistent: false);

        Assert.True(result.IsNotAllowed);
    }
}
