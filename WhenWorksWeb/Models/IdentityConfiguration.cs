using Microsoft.AspNetCore.Identity;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Models;

/// <summary>
/// Provides configuration settings for ASP.NET Core Identity options.
/// </summary>
/// <remarks>This class centralizes the configuration of Identity options such as password requirements,
/// lockout policies, and user settings. It is intended to be used during application startup to ensure consistent
/// Identity behavior across the application.</remarks>
public static class IdentityConfiguration
{
    /// <summary>
    /// Applies the application's sign-in, password, lockout, and username policies to the given Identity options.
    /// </summary>
    /// <param name="options">The Identity options to configure, typically supplied by <c>AddIdentity</c>.</param>
    public static void Configure(IdentityOptions options)
    {
        // Sign in settings
        options.SignIn.RequireConfirmedAccount = false;

        // Password settings. Kept in sync with ModelConstants.PasswordMinLength/
        // PasswordComplexityPattern, which the Manage/ChangePassword and Manage/SetPassword pages
        // use to surface these same rules to the user client-side before they ever hit this
        // server-side enforcement (see Spec/Features/FEATURES-tighten-account-validation.ospec).
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = ModelConstants.PasswordMinLength;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 3;

        // Lockout settings
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
    }
}
