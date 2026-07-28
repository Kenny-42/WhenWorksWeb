using Microsoft.AspNetCore.Identity;

namespace WhenWorksWeb.Models;

/// <summary>
/// Provides configuration settings for ASP.NET Core Identity options.
/// </summary>
/// <remarks>This class centralizes the configuration of Identity options such as password requirements,
/// lockout policies, and user settings. It is intended to be used during application startup to ensure consistent
/// Identity behavior across the application.</remarks>
public static class IdentityConfiguration
{
    public static void Configure(IdentityOptions options)
    {
        // Sign in settings
        options.SignIn.RequireConfirmedAccount = false;

        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
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
