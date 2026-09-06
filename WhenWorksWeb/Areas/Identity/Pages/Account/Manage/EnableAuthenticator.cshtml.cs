// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Sets up (or resets) an authenticator app: shows the shared TOTP key as both a scannable QR
    /// code and a manual-entry string, then confirms enabling 2FA by validating a code the user
    /// types back in from their app. The QR code is rendered entirely server-side via
    /// <see cref="QRCoder"/> (a small, offline, dependency-free renderer -- see
    /// Spec/Features/FEATURES-two-factor-authentication.ospec) and embedded as a data: URI, so the
    /// shared secret never passes through any client-side JavaScript.
    /// </summary>
    public class EnableAuthenticatorModel : PageModel
    {
        /// <summary>The issuer label shown in the authenticator app next to the account email.</summary>
        private const string AuthenticatorIssuer = "WhenWorks";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<EnableAuthenticatorModel> _logger;
        private readonly UrlEncoder _urlEncoder;

        public EnableAuthenticatorModel(
            UserManager<ApplicationUser> userManager,
            ILogger<EnableAuthenticatorModel> logger,
            UrlEncoder urlEncoder)
        {
            _userManager = userManager;
            _logger = logger;
            _urlEncoder = urlEncoder;
        }

        /// <summary>
        /// The shared key, formatted with spaces every 4 characters for easier manual entry.
        /// </summary>
        public string SharedKey { get; set; }

        /// <summary>
        /// A data: URI PNG of the otpauth:// QR code, ready to drop straight into an &lt;img src&gt;.
        /// </summary>
        public string QrCodeDataUri { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        /// Where to send the user once 2FA is enabled -- set when this page was reached via
        /// <see cref="Areas.Admin.RequireTwoFactorPageFilter"/>'s redirect, so an Admin lands back
        /// on the page they originally requested instead of always on TwoFactorAuthentication.
        /// Bound from the query string on GET and round-tripped through a hidden form field on
        /// POST (see EnableAuthenticator.cshtml).
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }

        [TempData]
        public string[] RecoveryCodes { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "The verification code is required.")]
            [StringLength(ModelConstants.TwoFactorCodeLength, MinimumLength = ModelConstants.TwoFactorCodeLength, ErrorMessage = "The verification code must be 6 digits.")]
            [RegularExpression(ModelConstants.TwoFactorCodePattern, ErrorMessage = "The verification code must be 6 digits.")]
            [DataType(DataType.Text)]
            [Display(Name = "Verification Code")]
            public string Code { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadSharedKeyAndQrCodeAsync(user);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadSharedKeyAndQrCodeAsync(user);
                return Page();
            }

            var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
                user, _userManager.Options.Tokens.AuthenticatorTokenProvider, Input.Code);

            if (!is2faTokenValid)
            {
                ModelState.AddModelError("Input.Code", "Verification code is invalid.");
                await LoadSharedKeyAndQrCodeAsync(user);
                return Page();
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            var userId = await _userManager.GetUserIdAsync(user);
            _logger.LogInformation("User with ID '{UserId}' has enabled 2FA with an authenticator app.", userId);

            StatusMessage = "Your authenticator app has been verified.";

            if (await _userManager.CountRecoveryCodesAsync(user) == 0)
            {
                var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
                RecoveryCodes = recoveryCodes.ToArray();
                return RedirectToPage("./ShowRecoveryCodes", new { returnUrl = ReturnUrl });
            }

            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl);
            }

            return RedirectToPage("./TwoFactorAuthentication");
        }

        /// <summary>
        /// Loads (generating one first if the user doesn't have one yet) the shared authenticator
        /// key and renders the QR code + manual-entry key for the view.
        /// </summary>
        private async Task LoadSharedKeyAndQrCodeAsync(ApplicationUser user)
        {
            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            SharedKey = FormatKey(unformattedKey);

            var email = await _userManager.GetEmailAsync(user);
            QrCodeDataUri = GenerateQrCodeDataUri(AuthenticatorIssuer, email, unformattedKey);
        }

        /// <summary>
        /// Splits the raw base32 key into groups of 4 characters, matching the default Identity
        /// scaffolding's manual-entry formatting.
        /// </summary>
        private static string FormatKey(string unformattedKey)
        {
            var result = new StringBuilder();
            var currentPosition = 0;
            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
                currentPosition += 4;
            }
            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition));
            }

            return result.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Builds the standard otpauth:// URI and renders it to a PNG QR code entirely server-side
        /// (via <see cref="PngByteQRCode"/>, which needs no System.Drawing/GDI+ dependency),
        /// returned as a data: URI ready for an &lt;img src&gt;.
        /// </summary>
        private string GenerateQrCodeDataUri(string issuer, string email, string unformattedKey)
        {
            var otpAuthUri = string.Format(
                "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits={3}",
                _urlEncoder.Encode(issuer),
                _urlEncoder.Encode(email),
                unformattedKey,
                ModelConstants.TwoFactorCodeLength);

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
            var pngQrCode = new PngByteQRCode(qrCodeData);
            var pngBytes = pngQrCode.GetGraphic(10);

            return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
        }
    }
}
