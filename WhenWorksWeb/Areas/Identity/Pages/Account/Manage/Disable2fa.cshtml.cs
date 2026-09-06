// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Disables 2FA without clearing the stored authenticator key -- re-enabling later
    /// (<see cref="TwoFactorAuthenticationModel"/>'s "Reset authenticator app" link) can reuse the
    /// same key rather than requiring a fresh QR scan. See <see cref="ResetAuthenticatorModel"/>
    /// for the variant that does clear it.
    /// </summary>
    public class Disable2faModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<Disable2faModel> _logger;

        public Disable2faModel(
            UserManager<ApplicationUser> userManager,
            ILogger<Disable2faModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGet()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!await _userManager.GetTwoFactorEnabledAsync(user))
            {
                throw new InvalidOperationException($"Cannot disable 2FA for user with ID '{_userManager.GetUserId(User)}' as it's not currently enabled.");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var disable2faResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
            if (!disable2faResult.Succeeded)
            {
                throw new InvalidOperationException($"Unexpected error occurred disabling 2FA for user with ID '{_userManager.GetUserId(User)}'.");
            }

            var userId = await _userManager.GetUserIdAsync(user);
            _logger.LogInformation("User with ID '{UserId}' has disabled 2fa.", userId);
            StatusMessage = "2fa has been disabled. You can reenable 2fa when you setup an authenticator app.";
            return RedirectToPage("./TwoFactorAuthentication");
        }
    }
}
