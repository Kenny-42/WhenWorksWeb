// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Disables 2FA and clears the stored authenticator key, so re-enabling requires scanning a
    /// fresh QR code -- distinct from <see cref="Disable2faModel"/>, which turns 2FA off without
    /// discarding the key.
    /// </summary>
    public class ResetAuthenticatorModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<ResetAuthenticatorModel> _logger;

        public ResetAuthenticatorModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<ResetAuthenticatorModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public IActionResult OnGet() => Page();

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.ResetAuthenticatorKeyAsync(user);

            // Invalidate any previously-issued recovery codes -- matching this page's own copy
            // ("will also reset your recovery codes"). Generating a set of zero replaces
            // whatever codes existed with an empty set rather than leaving the old ones
            // redeemable via LoginWithRecoveryCode after the user believes they've reset 2FA.
            await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);

            var userId = await _userManager.GetUserIdAsync(user);
            _logger.LogInformation("User with ID '{UserId}' has reset their authentication app key.", userId);

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your authenticator app key has been reset, you will need to configure your authenticator app using the new key.";

            return RedirectToPage("./EnableAuthenticator");
        }
    }
}
