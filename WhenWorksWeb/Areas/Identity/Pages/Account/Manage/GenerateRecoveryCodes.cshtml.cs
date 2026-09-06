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
    /// Confirms and then regenerates a user's 2FA recovery codes, invalidating the previous set.
    /// </summary>
    public class GenerateRecoveryCodesModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<GenerateRecoveryCodesModel> _logger;

        public GenerateRecoveryCodesModel(
            UserManager<ApplicationUser> userManager,
            ILogger<GenerateRecoveryCodesModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        [TempData]
        public string StatusMessage { get; set; }

        [TempData]
        public string[] RecoveryCodes { get; set; }

        /// <summary>
        /// Confirms the current page has an authenticator app already enabled -- generating fresh
        /// recovery codes for an account with 2FA off would be meaningless.
        /// </summary>
        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!await _userManager.GetTwoFactorEnabledAsync(user))
            {
                throw new InvalidOperationException($"Cannot generate recovery codes for user with ID '{_userManager.GetUserId(User)}' because they do not have 2FA enabled.");
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

            if (!await _userManager.GetTwoFactorEnabledAsync(user))
            {
                throw new InvalidOperationException($"Cannot generate recovery codes for user with ID '{_userManager.GetUserId(User)}' because they do not have 2FA enabled.");
            }

            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            RecoveryCodes = recoveryCodes.ToArray();

            var userId = await _userManager.GetUserIdAsync(user);
            _logger.LogInformation("User with ID '{UserId}' has generated new 2FA recovery codes.", userId);

            StatusMessage = "You have generated new recovery codes.";
            return RedirectToPage("./ShowRecoveryCodes");
        }
    }
}
