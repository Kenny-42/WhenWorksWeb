using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Services;

/// <summary>
/// Generates an email-confirmation token, builds the callback URL to Identity's scaffolded
/// ConfirmEmail page, and sends the confirmation email -- the sequence Register.cshtml.cs,
/// ResendEmailConfirmation.cshtml.cs, and ExternalLogin.cshtml.cs each used to carry an independent
/// copy of before this extraction (see
/// Spec/Refactors/REFACTOR-signin-manager-and-confirmation-email.ospec).
/// </summary>
/// <remarks>
/// Uses <see cref="LinkGenerator"/> rather than a Razor Page's <c>IUrlHelper</c> to build the
/// callback URL, since this is a plain injectable service, not a <c>PageModel</c> --
/// <c>LinkGenerator</c> is the framework's DI-friendly equivalent and needs an explicit
/// <c>area</c> value in <paramref name="values"/> below rather than inheriting one from ambient
/// route data, unlike a page's own <c>Url.Page</c> call. The caller's own <c>HttpContext</c> is
/// taken as an explicit parameter (rather than resolved from <c>IHttpContextAccessor</c>) so this
/// works the same whether it's called from a real request pipeline or a test that builds a
/// <c>PageModel</c> directly without one -- see <c>PageModelTestContext.AttachContext</c>, which
/// never populates <c>IHttpContextAccessor</c>'s ambient state.
/// </remarks>
public class EmailConfirmationLinkSender(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    LinkGenerator linkGenerator)
{
    /// <summary>
    /// Sends <paramref name="user"/> a confirmation-email link for their current email address.
    /// </summary>
    /// <param name="user">The user to confirm, already persisted (or about to be, with
    /// <see cref="UserManager{TUser}.GetUserIdAsync"/> already resolvable) with the email address to
    /// send to.</param>
    /// <param name="httpContext">The calling page/controller's own <c>HttpContext</c>, used to
    /// resolve the callback URL's scheme/host and routing services.</param>
    /// <param name="returnUrl">Optional return URL forwarded through to the confirmation callback,
    /// matching what Register.cshtml.cs/ExternalLogin.cshtml.cs previously passed by hand.</param>
    public async Task SendAsync(ApplicationUser user, HttpContext httpContext, string? returnUrl = null)
    {
        var userId = await userManager.GetUserIdAsync(user);
        var email = await userManager.GetEmailAsync(user)
            ?? throw new InvalidOperationException($"{nameof(EmailConfirmationLinkSender)} requires the user to have an email address set.");

        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var callbackUrl = linkGenerator.GetUriByPage(
            httpContext,
            page: "/Account/ConfirmEmail",
            values: new { area = "Identity", userId, code, returnUrl },
            scheme: httpContext.Request.Scheme)
            ?? throw new InvalidOperationException("Could not build the email confirmation callback URL.");

        await emailSender.SendEmailAsync(email, "Confirm your email",
            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
    }
}
