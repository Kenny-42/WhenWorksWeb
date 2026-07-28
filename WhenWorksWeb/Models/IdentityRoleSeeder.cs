using Microsoft.AspNetCore.Identity;

namespace WhenWorksWeb.Models;

/// <summary>
/// Ensures the application's Identity roles exist in the database.
/// </summary>
public static class IdentityRoleSeeder
{
    /// <summary>
    /// The roles created by default when no explicit role list is supplied to <see cref="SeedRolesAsync"/>.
    /// </summary>
    public static readonly string[] DefaultRoles = ["Admin", "User"];

    /// <summary>
    /// Creates any of the given roles that do not already exist.
    /// </summary>
    /// <param name="roleManager">The Identity role manager used to check for and create roles.</param>
    /// <param name="roles">The roles to ensure exist, or null to use <see cref="DefaultRoles"/>.</param>
    public static async Task SeedRolesAsync(
        RoleManager<IdentityRole> roleManager,
        IEnumerable<string>? roles = null)
    {
        roles ??= DefaultRoles;

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}
