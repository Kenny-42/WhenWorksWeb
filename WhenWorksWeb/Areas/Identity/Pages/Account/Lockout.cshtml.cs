#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Landing page for a sign-in attempt (password, 2FA code, or recovery code) against an account
    /// that's currently locked out under the policy configured in IdentityConfiguration.cs. Reached
    /// only after credentials have already been verified as correct -- see Login.cshtml.cs's
    /// <see cref="LockedOutUserName"/> remarks for why that's safe to disclose here -- so no session
    /// is ever established for the account behind it.
    /// </summary>
    [AllowAnonymous]
    public class LockoutModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public LockoutModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// Set by Login.cshtml.cs/LoginWith2fa.cshtml.cs/LoginWithRecoveryCode.cshtml.cs immediately
        /// before redirecting here. The property name has to match theirs -- the <see cref="TempDataAttribute"/>
        /// keys entries by property name, not by declaring type.
        /// </summary>
        [TempData]
        public string LockedOutUserName { get; set; }

        /// <summary>
        /// How long until the account's lockout expires, rounded up to the nearest whole minute for
        /// display. Null when there's nothing to show: no username was handed off (e.g. the page was
        /// requested directly), the account no longer exists, lockout isn't supported for it, or a
        /// narrow timing race where the lockout already expired between the redirect and this
        /// request.
        /// </summary>
        public TimeSpan? TimeRemaining { get; set; }

        public async Task OnGetAsync()
        {
            if (string.IsNullOrEmpty(LockedOutUserName))
            {
                return;
            }

            var user = await _userManager.FindByNameAsync(LockedOutUserName);
            if (user == null)
            {
                return;
            }

            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            if (lockoutEnd == null)
            {
                return;
            }

            var remaining = lockoutEnd.Value - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            // Round up so e.g. "12.3 seconds remaining" doesn't display as "0 minutes remaining".
            TimeRemaining = TimeSpan.FromMinutes(Math.Ceiling(remaining.TotalMinutes));
        }
    }
}
