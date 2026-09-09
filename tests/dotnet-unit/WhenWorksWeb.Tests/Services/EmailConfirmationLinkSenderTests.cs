using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Models;
using WhenWorksWeb.Services;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Services;

/// <summary>
/// Tier 3 tests for <see cref="EmailConfirmationLinkSender"/> -- the shared confirmation-email
/// sequence Register.cshtml.cs, ResendEmailConfirmation.cshtml.cs, and ExternalLogin.cshtml.cs all
/// call instead of each carrying their own copy (see
/// Spec/Refactors/REFACTOR-signin-manager-and-confirmation-email.ospec). Resolves a real, DI-wired
/// instance (real <see cref="UserManager{TUser}"/>, real <see cref="LinkGenerator"/>) with
/// <see cref="TestEmailSender"/> standing in for <c>BrevoEmailSender</c>, matching every other Tier 3
/// Identity test in this suite -- not a hand-built instance with mocked collaborators.
/// </summary>
public class EmailConfirmationLinkSenderTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EmailConfirmationLinkSenderTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // Ensures the app (and its endpoint routing, which GetUriByPage below resolves against) is
        // actually built before a test resolves services from a bare CreateScope() below, matching
        // ApplicationSignInManagerTests/ExternalLoginTests' pattern.
        _ = factory.Server;
    }

    [Fact]
    public async Task SendAsync_WithConfirmedRecipient_SendsEmailWithConfirmEmailCallbackLink()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var sender = services.GetRequiredService<EmailConfirmationLinkSender>();

        var user = new ApplicationUserBuilder().WithUserName("confirmationlinkuser").WithEmail("confirmationlinkuser@example.com").Build();
        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");

        await sender.SendAsync(user, httpContext);

        var sent = Assert.Single(_factory.EmailSender.SentEmails, e => e.Email == "confirmationlinkuser@example.com");
        Assert.Equal("Confirm your email", sent.Subject);
        Assert.Contains("/Identity/Account/ConfirmEmail", sent.HtmlMessage);
        var userId = await userManager.GetUserIdAsync(user);
        Assert.Contains($"userId={userId}", sent.HtmlMessage);
        Assert.Contains("code=", sent.HtmlMessage);
    }

    [Fact]
    public async Task SendAsync_WithReturnUrl_IncludesItInTheCallbackLink()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var sender = services.GetRequiredService<EmailConfirmationLinkSender>();

        var user = new ApplicationUserBuilder().WithUserName("confirmationlinkreturnurluser").WithEmail("confirmationlinkreturnurluser@example.com").Build();
        var createResult = await userManager.CreateAsync(user);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");

        await sender.SendAsync(user, httpContext, returnUrl: "/some/return/path");

        var sent = Assert.Single(_factory.EmailSender.SentEmails, e => e.Email == "confirmationlinkreturnurluser@example.com");
        Assert.Contains(Uri.EscapeDataString("/some/return/path"), sent.HtmlMessage);
    }

    [Fact]
    public async Task SendAsync_WithNoEmailSet_Throws()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var sender = services.GetRequiredService<EmailConfirmationLinkSender>();

        // Deliberately not persisted via CreateAsync (which requires an email, given
        // RequireUniqueEmail) -- an in-memory-only user with no email set at all, to exercise the
        // guard against a caller that hasn't set one yet.
        var user = new ApplicationUserBuilder().WithUserName("noemailconfirmationuser").Build();
        user.Email = null;

        var httpContext = new DefaultHttpContext { RequestServices = services };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(user, httpContext));
    }
}
