#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;
using WhenWorksWeb.Services;

namespace WhenWorksWeb.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Handles the round trip of an external (e.g. Google) sign-in: challenging the provider, processing its
    /// callback, signing in a user who has already linked that provider, and collecting this app's required
    /// custom fields (<see cref="ApplicationUser.DisplayName"/>, <see cref="ApplicationUser.Color"/>) from a
    /// provider identity that hasn't been linked to an <see cref="ApplicationUser"/> yet.
    /// </summary>
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<ExternalLoginModel> _logger;
        private readonly EmailConfirmationLinkSender _confirmationLinkSender;

        public ExternalLoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            ILogger<ExternalLoginModel> logger,
            EmailConfirmationLinkSender confirmationLinkSender)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _confirmationLinkSender = confirmationLinkSender;
        }

        /// <summary>
        /// Bound on the confirmation form shown to a brand-new external identity, collecting the custom fields
        /// this app requires that the provider doesn't supply.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        /// The URL to redirect to once sign-in (or account creation) completes.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        /// The display name of the external provider (e.g. "Google") shown on the confirmation form.
        /// </summary>
        [TempData]
        public string ProviderDisplayName { get; set; }

        /// <summary>
        /// Surfaced on the login page via <c>ErrorMessage</c> when the external sign-in attempt fails.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// The fields collected on the confirmation form for a first-time external identity, before its
        /// <see cref="ApplicationUser"/> is created.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            /// Created custom username field with the same validation Register.cshtml.cs uses, to ensure it only
            /// contains letters, numbers, and underscores. A provider identity doesn't supply a value that fits
            /// this rule (an email address contains characters <see cref="IdentityConfiguration.Configure"/>'s
            /// <c>AllowedUserNameCharacters</c> rejects), so this is always collected here rather than derived
            /// from <see cref="Email"/>.
            /// </summary>
            [Required]
            [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "The {0} can only contain letters and numbers.")]
            public string UserName { get; set; }

            /// <summary>
            /// The email address for the new account. Pre-filled from the provider's claims when available, but
            /// still editable since a provider isn't guaranteed to supply one (e.g. a Google Workspace account
            /// with a hidden email scope).
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            /// Stores the user's preferred display name for use in events, matching the same rule
            /// <see cref="ApplicationUser.DisplayName"/> enforces on the local registration form.
            /// </summary>
            [Required]
            [StringLength(ModelConstants.ApplicationUserDisplayNameMaxLength, MinimumLength = 1, ErrorMessage = "Display name must be between {2} and {1} characters.")]
            [RegularExpression(ModelConstants.DisplayNameContentPattern, ErrorMessage = "Display name can't be blank or made up only of whitespace, and can't contain control characters or invisible characters.")]
            [Display(Name = "Display Name")]
            public string DisplayName { get; set; }

            /// <summary>
            /// Stores a hexadecimal color code (without the '#' symbol) that represents the user's preferred
            /// personal color for use in events, matching the same rule <see cref="ApplicationUser.Color"/>
            /// enforces on the local registration form.
            /// </summary>
            [Required]
            [RegularExpression(ModelConstants.HexColorPattern, ErrorMessage = "The {0} must be a valid 6-character hexadecimal color code.")]
            [Display(Name = "Preferred Color")]
            public string Color { get; set; }
        }

        /// <summary>
        /// A GET to this page isn't a supported entry point — external sign-in is always initiated via the POST
        /// on Login/Register's provider button. Redirect back to Login rather than rendering an empty page.
        /// </summary>
        public IActionResult OnGet() => RedirectToPage("./Login");

        /// <summary>
        /// Issues the challenge that redirects the browser to the external provider (e.g. Google), called from
        /// the provider button's POST on Login.cshtml/Register.cshtml. The provider's own login screen returns
        /// control to <see cref="OnGetCallbackAsync"/> below.
        /// </summary>
        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        /// <summary>
        /// Handles the external provider's redirect back to the app. Signs the user in directly if this
        /// provider identity is already linked to an <see cref="ApplicationUser"/>; otherwise pre-fills the
        /// confirmation form (below) so the user can supply <see cref="ApplicationUser.DisplayName"/> and
        /// <see cref="ApplicationUser.Color"/> before their account is created.
        /// </summary>
        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Sign in the user with this external login provider if the user already has a linked account.
            // bypassTwoFactor is deliberately false -- a 2FA-enabled account signing in via Google must be
            // challenged for a TOTP/recovery code the same as a local password sign-in (see
            // Spec/Features/FEATURES-two-factor-authentication.ospec), not silently waved through.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: false);
            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity?.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl });
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }
            if (result.IsNotAllowed)
            {
                // This is the normal, expected outcome for any account created via
                // OnPostConfirmationAsync's email-mismatch branch below -- reached on every sign-in
                // attempt via this provider until that account's email is confirmed. It's also
                // reached for a linked account whose email was later unconfirmed via Manage/Email.
                // Redirecting here (rather than falling through to the "no account linked yet"
                // branch) avoids trying to create a duplicate account, which would fail with a
                // confusing "username already taken" error instead of this actionable redirect.
                var unconfirmedUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (unconfirmedUser == null)
                {
                    // Narrow race: the account was deleted/unlinked between ExternalLoginSignInAsync
                    // succeeding internally and this re-lookup. Surface a generic error instead of
                    // redirecting to RegisterConfirmation with a null email, which that page would
                    // otherwise treat as a missing parameter -- see
                    // Spec/Refactors/REFACTOR-email-verification-review-cleanup.ospec.
                    ErrorMessage = "Something went wrong. Please try again.";
                    return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
                }
                return RedirectToPage("./RegisterConfirmation", new { email = unconfirmedUser.Email });
            }

            // No local account is linked to this external identity yet — collect the app's required custom
            // fields (DisplayName, Color) and finish creating the account on the confirmation form below.
            ReturnUrl = returnUrl;
            ProviderDisplayName = info.ProviderDisplayName;
            Input = new InputModel
            {
                Email = info.Principal.FindFirst(ClaimTypes.Email)?.Value,
                Color = ModelConstants.DefaultParticipantColor
            };
            return Page();
        }

        /// <summary>
        /// Handles the confirmation form's POST for a first-time external identity: creates the
        /// <see cref="ApplicationUser"/> with the submitted <see cref="ApplicationUser.DisplayName"/>/
        /// <see cref="ApplicationUser.Color"/>, links the external login to it, and either signs the
        /// user in directly (when the submitted email matches the provider's verified claim) or sends
        /// a normal confirmation-email link and redirects to RegisterConfirmation (when it doesn't).
        /// </summary>
        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            // Get the information about the user from the external login provider.
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                // Populate the custom properties this app requires beyond what the provider supplies.
                // DisplayName is trimmed and NFC-normalized here (not just validated) so
                // leading/trailing whitespace never reaches the database and two visually-identical
                // names that differ only in codepoint composition persist the same way, matching
                // Register.cshtml.cs/Manage/Index.cshtml.cs -- see
                // ModelConstants.DisplayNameContentPattern and TextNormalizer.
                user.DisplayName = TextNormalizer.NormalizeToNfc(Input.DisplayName?.Trim());
                user.Color = Input.Color;

                // CreatedAt/LastActiveAt are required properties with no meaningful default — set them
                // explicitly here rather than leaving them at their CLR default.
                var now = DateTime.UtcNow;
                user.CreatedAt = now;
                user.LastActiveAt = now;

                await _userStore.SetUserNameAsync(user, Input.UserName, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                // The external provider (Google) has verified ownership of whatever address it handed
                // back in info.Principal's email claim -- but only when it also says so via the
                // standard OIDC "email_verified" claim (mapped in Program.cs's AddGoogle setup); a
                // provider's "email" claim alone doesn't prove that address was ever confirmed with
                // Google itself. That verified claim is what can stand in for the confirmation-email
                // link a local registration requires. It does NOT extend to whatever the user typed
                // into Input.Email: that field is freely editable on the confirmation form
                // (ExternalLogin.cshtml), so trusting it unconditionally would let anyone who owns
                // *some* Google account claim EmailConfirmed=true on an address they don't own, just
                // by editing the field before submitting. Only mark the address confirmed when it's
                // exactly the one the provider vouched for, and the provider says that address is
                // itself verified.
                //
                // Compared case-insensitively against bool.TrueString: Program.cs's MapJsonKey(...,
                // ClaimValueTypes.Boolean) populates this claim via JsonKeyClaimAction, which stores a
                // JSON boolean's value as JsonElement.ToString() -- .NET's capitalized "True"/"False",
                // not the lowercase JSON literal spelling. A lowercase-only comparison here can never
                // match a claim actually produced this way -- see
                // Spec/Bugs/BUGS-email-verification-review-findings.ospec.
                var providerVerifiedEmail = info.Principal.FindFirst(ClaimTypes.Email)?.Value;
                var providerConfirmsEmailVerified = string.Equals(
                    info.Principal.FindFirst("email_verified")?.Value, bool.TrueString, StringComparison.OrdinalIgnoreCase);
                var emailIsProviderVerified = providerVerifiedEmail is not null
                    && providerConfirmsEmailVerified
                    && string.Equals(providerVerifiedEmail, Input.Email, StringComparison.OrdinalIgnoreCase);

                if (emailIsProviderVerified)
                {
                    await _emailStore.SetEmailConfirmedAsync(user, true, CancellationToken.None);
                }

                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                        if (emailIsProviderVerified)
                        {
                            // EmailConfirmed is already true above, so RequireConfirmedAccount won't
                            // block this -- see Spec/Features/FEATURES-email-verification.ospec.
                            await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                            return LocalRedirect(returnUrl);
                        }

                        // Input.Email didn't match the provider's claim (or the provider didn't supply
                        // one), so ownership is unverified: send the same confirmation-email link
                        // Register.cshtml.cs sends for a local account, and land on
                        // RegisterConfirmation instead of establishing a session for an unconfirmed
                        // address -- SignInAsync above bypasses RequireConfirmedAccount entirely, so
                        // it must never be called for an account that isn't actually confirmed.
                        await _confirmationLinkSender.SendAsync(user, HttpContext, returnUrl);

                        return RedirectToPage("./RegisterConfirmation", new { email = Input.Email });
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;
            return Page();
        }

        /// <summary>
        /// Creates a new, uninitialized <see cref="ApplicationUser"/> instance, matching the same pattern
        /// <c>Register.cshtml.cs</c> uses for the local registration flow.
        /// </summary>
        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the external login page in /Areas/Identity/Pages/Account/ExternalLogin.cshtml");
            }
        }

        /// <summary>
        /// Retrieves the <see cref="IUserEmailStore{TUser}"/> implementation from the user store, matching the
        /// same pattern <c>Register.cshtml.cs</c> uses for the local registration flow.
        /// </summary>
        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
