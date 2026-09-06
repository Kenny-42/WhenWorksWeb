// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WhenWorksWeb.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Displays a freshly (re)generated set of 2FA recovery codes exactly once -- they arrive via
    /// <see cref="RecoveryCodes"/>'s TempData round-trip from EnableAuthenticator/
    /// GenerateRecoveryCodes, so reloading or navigating back here directly shows nothing.
    /// </summary>
    public class ShowRecoveryCodesModel : PageModel
    {
        [TempData]
        public string[] RecoveryCodes { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        /// <summary>
        /// Where "Continue" should send the user, carried through from EnableAuthenticator's own
        /// ReturnUrl (e.g. back to the Admin page RequireTwoFactorPageFilter redirected from) when
        /// present; falls back to TwoFactorAuthentication otherwise.
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }

        public IActionResult OnGet()
        {
            if (RecoveryCodes == null || RecoveryCodes.Length == 0)
            {
                return RedirectToPage("./TwoFactorAuthentication");
            }

            return Page();
        }
    }
}
