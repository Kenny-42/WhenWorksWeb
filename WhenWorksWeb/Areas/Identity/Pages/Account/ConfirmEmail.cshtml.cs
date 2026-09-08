// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ConfirmEmailModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            // The stock scaffold redirects to a "/Index" Razor Page here, which doesn't exist in this
            // MVC-first app (there's no Pages/Index.cshtml -- Home/Index is an MVC controller action,
            // not a page) and would throw "No page named '/Index' matches the supplied values" the
            // first time this branch is actually hit, the same category of dead redirect
            // BUGS-registration-confirmation-redirect.ospec already fixed once. BadRequest instead,
            // matching ResetPassword.cshtml.cs's identical guard for a link missing its required code.
            if (userId == null || code == null)
            {
                return BadRequest("A user ID and code must be supplied to confirm an email.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{userId}'.");
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, code);
            StatusMessage = result.Succeeded ? "Thank you for confirming your email." : "Error confirming your email.";
            return Page();
        }
    }
}
