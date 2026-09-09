using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhenWorksWeb.Areas.Identity.Pages.Account;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;
using WhenWorksWeb.Services;
using WhenWorksWeb.Tests.Fixtures;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Tier 3 tests for <see cref="ExternalLoginModel.OnPostConfirmationAsync"/>'s two trust boundaries:
/// (1) the external provider (e.g. Google) only vouches for whatever address it put in the sign-in's
/// "Email" claim, not whatever the user typed into the confirmation form's freely-editable Email field
/// (<see cref="ExternalLoginModel.InputModel.Email"/>), and (2) matching that claim alone doesn't prove
/// the address was ever verified with the provider -- that's a separate fact carried by the standard
/// OIDC "email_verified" claim. Guards against the account-creation path silently marking an
/// unverified address <see cref="ApplicationUser.EmailConfirmed"/> -- see the fix's comment in
/// ExternalLogin.cshtml.cs's <c>OnPostConfirmationAsync</c> and
/// Spec/Bugs/BUGS-external-login-email-verification-claim.ospec.
/// </summary>
/// <remarks>
/// Drives the real page handler (unlike <see cref="ExternalLoginTests"/>, which exercises
/// <see cref="SignInManager{TUser}.ExternalLoginSignInAsync(string, string, bool)"/> directly) since
/// the behavior under test is <c>OnPostConfirmationAsync</c>'s own branching on the provider's claim,
/// not the sign-in check downstream of it. <see cref="FakeExternalLoginSignInManager"/> stands in for
/// the real <see cref="SignInManager{TUser}.GetExternalLoginInfoAsync"/> call, which normally reads the
/// "Identity.External" cookie set by an actual OAuth round trip -- reproducing that handshake here
/// would test ASP.NET Core's own OAuth handler, not this page's logic.
/// </remarks>
public class ExternalLoginConfirmationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string LoginProvider = "Google";

    private readonly CustomWebApplicationFactory _factory;

    public ExternalLoginConfirmationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Builds a real, request-scoped <see cref="ExternalLoginModel"/> whose
    /// <see cref="SignInManager{TUser}.GetExternalLoginInfoAsync"/> returns a canned
    /// <see cref="ExternalLoginInfo"/> carrying <paramref name="providerClaimedEmail"/> as the
    /// provider's email claim and <paramref name="emailVerified"/> as its "email_verified" claim
    /// (mirroring Program.cs's AddGoogle claim mapping), so <c>OnPostConfirmationAsync</c> can be
    /// driven without a real OAuth round trip.
    /// </summary>
    private async Task<(ExternalLoginModel Model, IServiceScope Scope)> CreateModelAsync(string? providerClaimedEmail, bool emailVerified = true)
    {
        var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var userStore = services.GetRequiredService<IUserStore<ApplicationUser>>();
        var logger = services.GetRequiredService<ILogger<ExternalLoginModel>>();
        var confirmationLinkSender = services.GetRequiredService<EmailConfirmationLinkSender>();

        // "email_verified" is built with bool.TrueString/FalseString ("True"/"False"), not the
        // lowercase JSON literal spelling, matching what Program.cs's MapJsonKey(...,
        // ClaimValueTypes.Boolean) actually produces via JsonElement.ToString() -- see
        // Spec/Bugs/BUGS-email-verification-review-findings.ospec.
        var claims = providerClaimedEmail is null
            ? []
            : new[]
            {
                new Claim(ClaimTypes.Email, providerClaimedEmail),
                new Claim("email_verified", emailVerified ? bool.TrueString : bool.FalseString)
            };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: LoginProvider));
        var externalLoginInfo = new ExternalLoginInfo(principal, LoginProvider, providerKey: Guid.NewGuid().ToString(), LoginProvider);

        var signInManager = new FakeExternalLoginSignInManager(
            externalLoginInfo,
            userManager,
            services.GetRequiredService<IHttpContextAccessor>(),
            services.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            services.GetRequiredService<IOptions<IdentityOptions>>(),
            services.GetRequiredService<ILogger<SignInManager<ApplicationUser>>>(),
            services.GetRequiredService<IAuthenticationSchemeProvider>(),
            services.GetRequiredService<IUserConfirmation<ApplicationUser>>());

        var model = new ExternalLoginModel(signInManager, userManager, userStore, logger, confirmationLinkSender);

        // Full app RequestServices (not the isolated minimal one PageModelTestContext builds by
        // default) so SignInAsync's cookie authentication -- data protection, the auth service -- can
        // actually resolve, matching ExternalLoginTests' CreateLinkedGoogleUserAsync. It's also what
        // EmailConfirmationLinkSender's IHttpContextAccessor/LinkGenerator resolve against, so the
        // confirmation callback URL in the mismatch/no-claim tests below builds for real.
        PageModelTestContext.AttachContext(model, requestServices: services);
        signInManager.Context = model.HttpContext;

        return (model, scope);
    }

    private static ExternalLoginModel.InputModel BuildInput(string userName, string email) => new()
    {
        UserName = userName,
        Email = email,
        DisplayName = "Test User",
        Color = ModelConstants.DefaultParticipantColor
    };

    /// <summary>
    /// The safe case: the submitted email matches exactly what the provider vouched for, so it's
    /// trusted -- the account is created already confirmed and the user is signed straight in.
    /// </summary>
    [Fact]
    public async Task Confirmation_WithEmailMatchingProviderClaim_CreatesConfirmedAccountAndSignsIn()
    {
        var (model, scope) = await CreateModelAsync(providerClaimedEmail: "verified@example.com");
        using (scope)
        {
            model.Input = BuildInput("matchingemailuser", "verified@example.com");

            var result = await model.OnPostConfirmationAsync();

            Assert.IsType<LocalRedirectResult>(result);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByNameAsync("matchingemailuser");
            Assert.NotNull(user);
            Assert.True(user!.EmailConfirmed);

            // Not Assert.Empty(SentEmails) -- EmailSender is one singleton shared across every test in
            // this class (see CustomWebApplicationFactory.EmailSender), so other tests' sends can
            // already be in the collection by the time this runs. What matters here is that *this*
            // address never triggered a send.
            Assert.DoesNotContain(_factory.EmailSender.SentEmails, sent => sent.Email == "verified@example.com");
        }
    }

    /// <summary>
    /// The exploit this fix closes: signing in with a real Google account only proves ownership of
    /// *that* account's email -- editing the confirmation form's Email field to a different address
    /// before submitting must not mark that other, unverified address confirmed, and must not sign the
    /// user in under it without proof of ownership.
    /// </summary>
    [Fact]
    public async Task Confirmation_WithEmailNotMatchingProviderClaim_CreatesUnconfirmedAccountAndSendsConfirmationEmail()
    {
        var (model, scope) = await CreateModelAsync(providerClaimedEmail: "attacker@example.com");
        using (scope)
        {
            model.Input = BuildInput("mismatchedemailuser", "victim@example.com");

            var result = await model.OnPostConfirmationAsync();

            var redirect = Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("./RegisterConfirmation", redirect.PageName);
            Assert.Equal("victim@example.com", redirect.RouteValues?["email"]);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByNameAsync("mismatchedemailuser");
            Assert.NotNull(user);
            Assert.False(user!.EmailConfirmed);

            // See the matching-claim test for why this filters by address rather than asserting on the
            // whole (class-fixture-shared) collection.
            Assert.Single(_factory.EmailSender.SentEmails, sent => sent.Email == "victim@example.com");
        }
    }

    /// <summary>
    /// A second trust boundary this fix closes: matching the "Email" claim only proves the user
    /// didn't retype a different address on the form -- it does not prove Google itself ever verified
    /// that address. When Google's own "email_verified" claim says otherwise (or is missing), the
    /// account must not be pre-confirmed even though the submitted email matches exactly -- see
    /// Spec/Bugs/BUGS-external-login-email-verification-claim.ospec.
    /// </summary>
    [Fact]
    public async Task Confirmation_WithMatchingEmailButUnverifiedByProvider_CreatesUnconfirmedAccountAndSendsConfirmationEmail()
    {
        var (model, scope) = await CreateModelAsync(providerClaimedEmail: "unverified@example.com", emailVerified: false);
        using (scope)
        {
            model.Input = BuildInput("unverifiedemailuser", "unverified@example.com");

            var result = await model.OnPostConfirmationAsync();

            var redirect = Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("./RegisterConfirmation", redirect.PageName);
            Assert.Equal("unverified@example.com", redirect.RouteValues?["email"]);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByNameAsync("unverifiedemailuser");
            Assert.NotNull(user);
            Assert.False(user!.EmailConfirmed);

            Assert.Single(_factory.EmailSender.SentEmails, sent => sent.Email == "unverified@example.com");
        }
    }

    /// <summary>
    /// Same unverified-ownership case as above, but for a provider identity that supplies no email
    /// claim at all (e.g. a restricted-scope Google Workspace account) -- there's nothing to compare
    /// against, so the submitted address can never be trusted by construction.
    /// </summary>
    [Fact]
    public async Task Confirmation_WithNoProviderEmailClaim_CreatesUnconfirmedAccount()
    {
        var (model, scope) = await CreateModelAsync(providerClaimedEmail: null);
        using (scope)
        {
            model.Input = BuildInput("noemailclaimuser", "someone@example.com");

            var result = await model.OnPostConfirmationAsync();

            Assert.IsType<RedirectToPageResult>(result);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByNameAsync("noemailclaimuser");
            Assert.NotNull(user);
            Assert.False(user!.EmailConfirmed);
        }
    }

    /// <summary>
    /// Standing in for <see cref="SignInManager{TUser}.GetExternalLoginInfoAsync"/>'s real "Identity.External"
    /// cookie read -- see this test class's remarks.
    /// </summary>
    private sealed class FakeExternalLoginSignInManager(
        ExternalLoginInfo? externalLoginInfo,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation)
        : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        public override Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(string? expectedXsrf = null)
            => Task.FromResult(externalLoginInfo);
    }
}
