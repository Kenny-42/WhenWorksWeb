// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Lost-device sign-in fallback: completes sign-in using one of a 2FA-enabled account's
    /// recovery codes instead of a live TOTP code, reachable from <c>LoginWith2fa.cshtml</c>.
    /// </summary>
    public class LoginWithRecoveryCodeModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginWithRecoveryCodeModel> _logger;

        public LoginWithRecoveryCodeModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginWithRecoveryCodeModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        /// <summary>
        /// Carries the username through to Lockout.cshtml.cs's <c>OnGetAsync</c> so it can display
        /// the account's remaining lockout time -- see Login.cshtml.cs's identically-named property
        /// for the full rationale (TempData, not a query-string route value). Safe here for the same
        /// reason: this branch is only reached after the account has already been identified via
        /// <see cref="GetTwoFactorAuthenticationUserAsync"/>.
        /// </summary>
        [TempData]
        public string LockedOutUserName { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "The recovery code is required.")]
            [StringLength(ModelConstants.RecoveryCodeMaxLength, ErrorMessage = "The recovery code is too long.")]
            [DataType(DataType.Text)]
            [Display(Name = "Recovery Code")]
            public string RecoveryCode { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string returnUrl = null)
        {
            // Ensure the user has gone through the username & password screen first.
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new InvalidOperationException("Unable to load two-factor authentication user.");
            }

            ReturnUrl = returnUrl;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                throw new InvalidOperationException("Unable to load two-factor authentication user.");
            }

            var userId = await _userManager.GetUserIdAsync(user);

            // Unlike TwoFactorAuthenticatorSignInAsync (which runs PreSignInCheck, and so already
            // catches this), the base SignInManager.TwoFactorRecoveryCodeSignInAsync never consults
            // lockout at all -- by design, per its own "we don't protect against brute force attacks
            // since codes are expected to be random" comment. Without this check, a still-locked-out
            // account could sign in with a valid recovery code despite the lockout redirects everywhere
            // else on this flow, and a wrong code would never count towards it either. Checked before
            // attempting redemption so a locked-out user's recovery code isn't burned for nothing.
            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("User with ID '{UserId}' account locked out.", userId);
                LockedOutUserName = await _userManager.GetUserNameAsync(user);
                return RedirectToPage("./Lockout");
            }

            // Strip whitespace some users incidentally include when copying a code such as
            // "abcde-12345" -- redemption itself doesn't care about the hyphen either way.
            var recoveryCode = Input.RecoveryCode.Replace(" ", string.Empty);

            var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);

            if (result.Succeeded)
            {
                _logger.LogInformation("User with ID '{UserId}' logged in with a recovery code.", userId);
                return LocalRedirect(returnUrl ?? Url.Content("~/"));
            }

            // No result.IsLockedOut branch here (unlike Login.cshtml.cs/LoginWith2fa.cshtml.cs): per
            // the remarks above, TwoFactorRecoveryCodeSignInAsync itself never returns IsLockedOut --
            // the explicit check before redemption is what covers that case for this page instead.
            _logger.LogWarning("Invalid recovery code entered for user with ID '{UserId}'.", userId);
            ModelState.AddModelError(string.Empty, "Invalid recovery code entered.");
            return Page();
        }
    }
}
