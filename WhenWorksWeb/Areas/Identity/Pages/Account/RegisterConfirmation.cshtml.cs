#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WhenWorksWeb.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Landing page for a registration, login (local password or external provider), or
    /// account-creation attempt against an account whose email isn't confirmed yet. Reached only
    /// after the account has been created or its credentials already verified as correct (see
    /// Register.cshtml.cs's <c>OnPostAsync</c>, Login.cshtml.cs's <c>OnPostAsync</c>, and
    /// ExternalLogin.cshtml.cs's <c>OnGetCallbackAsync</c>/<c>OnPostConfirmationAsync</c>), so no
    /// session is ever established for the account behind it.
    /// </summary>
    /// <remarks>
    /// This is the stock Identity-scaffolded page, kept intentionally rather than the hand-written
    /// ConfirmEmailRequired page it replaces -- see Spec/Bugs/BUGS-registration-confirmation-redirect.ospec.
    /// Unlike the stock template, it never generates or displays a raw confirmation link: confirming
    /// by actually clicking the emailed link is a hard requirement here, not a testing convenience
    /// to preserve behind a Development check.
    /// </remarks>
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        /// <summary>The email address to show on the page, echoing back what the user just entered.</summary>
        public string Email { get; set; }

        /// <summary>
        /// Set when <paramref name="email"/> is missing/empty, so the view renders a generic error
        /// state instead of the normal "check your email" message. A caller can reach this page with
        /// no <c>email</c> either directly (no natural "previous page" to bounce back to) or via the
        /// narrow post-sign-in re-lookup race in Login.cshtml.cs/ExternalLogin.cshtml.cs -- either way
        /// this should fail visibly rather than silently redirect to /Index, which gave no indication
        /// anything had gone wrong. See Spec/Refactors/REFACTOR-email-verification-review-cleanup.ospec.
        /// </summary>
        public bool HasError { get; set; }

        /// <summary>
        /// Renders unconditionally on any non-empty <paramref name="email"/>, without looking the
        /// address up against the user store: whether that lookup finds an account is itself
        /// observable through the response (a 200 vs. a 404), turning this otherwise-unauthenticated
        /// endpoint into an account-existence oracle. <paramref name="email"/> is echoed back as
        /// submitted rather than a value sourced from a lookup, so no branch of the response depends
        /// on whether the account exists -- matching ResendEmailConfirmation.cshtml.cs's identical
        /// message regardless of account existence. See
        /// Spec/Bugs/BUGS-email-verification-review-findings.ospec.
        /// </summary>
        public IActionResult OnGet(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                HasError = true;
                return Page();
            }

            Email = email;

            return Page();
        }
    }
}
