// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            /// Created custom username field with validation to ensure it only contains letters, numbers, and underscores. 
            /// This allows users to have a unique identifier that is different from their email address.
            /// </summary>
            [Required]
            [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "The {0} can only contain letters and numbers.")]
            public string UserName { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            /// Stores the user's preferred display name for use in events, which can be different from their username. 
            /// This allows users to have a more personalized and friendly name shown in the application, especially in event-related contexts. 
            /// The maximum length is set to 16 characters to ensure concise display names.
            /// </summary>
            [Required]
            [StringLength(16, ErrorMessage = "The {0} must be at least {1} character long.", MinimumLength = 1)]
            [Display(Name = "Display Name")]
            public string DisplayName { get; set; }

            /// <summary>
            /// Stores a hexadecimal color code (without the '#' symbol) that represents the user's preferred personal color for use in events.
            /// Has a default value of "ff66c4" (a shade of pink) to ensure that users have a color assigned even if they don't specify one
            /// during registration.
            /// </summary>
            [Required]
            [RegularExpression(@"^[A-Fa-f0-9]{6}$", ErrorMessage = "The {0} must be a valid 6-character hexadecimal color code.")]
            [Display(Name = "Preferred Color")]
            public string Color { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        /// <summary>
        /// This method is called when the registration page is accessed via a GET request. It initializes the Input model 
        /// with a default color value and retrieves the list of external authentication schemes to display on the registration page. 
        /// The returnUrl parameter is used to redirect the user after successful registration.
        /// </summary>
        /// <param name="returnUrl"></param>
        /// <returns></returns>
        public async Task OnGetAsync(string returnUrl = null)
        {
            // Set the return URL to redirect the user after successful registration
            ReturnUrl = returnUrl;

            // Set a default color value for the registration form
            Input = new InputModel
            {
                Color = "ff66c4"
            };

            // Retrieve the list of external authentication schemes (e.g., Google, Facebook) to display on the registration page
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        /// <summary>
        /// This method is called when the registration form is submitted via a POST request. It validates the input, creates a 
        /// new user with the provided information, and handles the registration process. If the registration is successful, 
        /// it sends a confirmation email to the user and either redirects to a confirmation page or signs the user in directly 
        /// based on the application's configuration. If there are errors during registration, it redisplays the form with error 
        /// messages.
        /// </summary>
        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            // Set the return URL to redirect the user after successful registration
            returnUrl ??= Url.Content("~/");

            // Retrieve the list of external authentication schemes (e.g., Google, Facebook) to display on the registration page
            // in case of validation failure
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                // Populate the custom properties
                user.UserName = Input.UserName;
                user.DisplayName = Input.DisplayName;
                user.Color = Input.Color;

                await _userStore.SetUserNameAsync(user, Input.UserName, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                // If the user creation is successful, log the event and send a confirmation email.
                // Depending on the application's configuration, either redirect to a confirmation page or sign the user in directly.
                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    // If the application requires confirmed accounts, redirect to the confirmation page.
                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    // If account confirmation is not required, sign the user in directly and redirect to the return URL.
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                // If there are errors during user creation, add them to the ModelState to display on the registration form.
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        /// <summary>
        /// This method creates a new instance of the ApplicationUser class. It uses Activator.CreateInstance to create the instance, 
        /// which requires that the ApplicationUser class has a parameterless constructor and is not abstract. If the instance cannot
        /// be created, it throws an InvalidOperationException with a message indicating the issue and suggesting how to resolve it.
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
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        /// <summary>
        /// This method retrieves the IUserEmailStore implementation from the user store. It checks if the user manager supports 
        /// user email, and if not, it throws a NotSupportedException indicating that the default UI requires a user store with 
        /// email support. If the user store does support email, it casts the user store to IUserEmailStore<ApplicationUser> and 
        /// returns it for use in managing user email addresses during registration.
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
