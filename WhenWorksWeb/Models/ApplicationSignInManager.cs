using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WhenWorksWeb.Models;

/// <summary>
/// Overrides <see cref="SignInManager{TUser}.CheckPasswordSignInAsync"/> so the password is verified
/// BEFORE any lockout/confirmation status is consulted, instead of the base implementation's order
/// (status first, then password). Registered in place of the stock <see cref="SignInManager{TUser}"/>
/// via <c>AddSignInManager&lt;ApplicationSignInManager&gt;()</c> in Program.cs.
/// </summary>
/// <remarks>
/// This closes a username-enumeration oracle: with the base ordering, a wrong password against a
/// locked-out or unconfirmed account returns the same <see cref="SignInResult"/> as a correct
/// password would, letting an attacker who doesn't know the password learn that the account exists
/// and is locked out/unconfirmed. Login.cshtml.cs previously worked around this by hand (see
/// Spec/Refactors/REFACTOR-signin-manager-and-confirmation-email.ospec's motivation), which
/// duplicated <see cref="SignInManager{TUser}.PreSignInCheck"/>'s internal ordering, hashed the
/// password twice per successful login, and never actually closed the lockout half of the gap. This
/// override is the idiomatic Identity extension point for exactly this kind of reordering: it
/// centralizes the fix in the one place the framework designed for it, applies to every caller (not
/// just Login.cshtml.cs), and hashes the password once.
/// </remarks>
public class ApplicationSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    // Mirrors Microsoft.AspNetCore.Identity.EventIds.InvalidPassword (Id: 2) -- that type is
    // internal to the framework assembly, so it can't be referenced directly here. Reproducing its
    // Id/Name lets log processing that filters/groups by EventId treat this the same way it would
    // the base SignInManager's own wrong-password log entry. See
    // Spec/Refactors/REFACTOR-email-verification-review-cleanup.ospec.
    private static readonly EventId InvalidPasswordEventId = new(2, "InvalidPassword");

    /// <summary>
    /// Verifies <paramref name="password"/> first; only once it's correct does this consult
    /// <see cref="SignInManager{TUser}.PreSignInCheck"/> (confirmation, then lockout -- the
    /// framework's own ordering within that method is left untouched) for lockout/confirmation
    /// status. A wrong password always returns <see cref="SignInResult.Failed"/> immediately, with
    /// no status disclosed.
    /// </summary>
    public override async Task<SignInResult> CheckPasswordSignInAsync(ApplicationUser user, string password, bool lockoutOnFailure)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!await UserManager.CheckPasswordAsync(user, password))
        {
            // Reproduces the base SignInManager's own wrong-password diagnostic log, dropped by this
            // override otherwise -- an operator grepping logs for failed-password attempts against a
            // specific account (e.g. investigating a brute-force/credential-stuffing pattern) would
            // otherwise find nothing from this app. See
            // Spec/Refactors/REFACTOR-email-verification-review-cleanup.ospec.
            Logger.LogDebug(InvalidPasswordEventId, "User failed to provide the correct password.");

            // Mirrors the base implementation's failure branch (access-failed counting only when
            // lockoutOnFailure is requested) -- Login.cshtml.cs always passes lockoutOnFailure: false,
            // so this branch is currently unreached in practice, but the override still needs to
            // behave correctly for any future caller that passes true. Changing this counting
            // behavior itself is out of scope for this refactor.
            if (UserManager.SupportsUserLockout && lockoutOnFailure)
            {
                var incrementLockoutResult = await UserManager.AccessFailedAsync(user) ?? IdentityResult.Success;
                if (!incrementLockoutResult.Succeeded)
                {
                    return SignInResult.Failed;
                }

                if (await UserManager.IsLockedOutAsync(user))
                {
                    return await LockedOut(user);
                }
            }

            return SignInResult.Failed;
        }

        var preSignInCheck = await PreSignInCheck(user);
        if (preSignInCheck != null)
        {
            return preSignInCheck;
        }

        // Matches the base SignInManager's own CheckPasswordSignInCoreAsync guard: only reset the
        // failed-access counter here when sign-in is actually complete -- i.e. 2FA is disabled for
        // this account, or this client already has it remembered. A 2FA-enabled account that still
        // needs its second factor challenged must not have its lockout counter reset until that
        // challenge is verified (LoginWith2fa.cshtml.cs / TwoFactorSignInAsync handle that reset) --
        // see Spec/Bugs/BUGS-email-verification-review-findings.ospec.
        if (!await IsTwoFactorEnabledAsync(user) || await IsTwoFactorClientRememberedAsync(user))
        {
            await ResetLockout(user);
        }
        return SignInResult.Success;
    }
}
