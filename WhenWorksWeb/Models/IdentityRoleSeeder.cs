using Microsoft.AspNetCore.Identity;

namespace WhenWorksWeb.Models;

public static class IdentityRoleSeeder
{
    public static readonly string[] DefaultRoles = ["Admin", "User"];

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

    public static async Task AddUserToRoleByEmailAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string roleName)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return;
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            await userManager.AddToRoleAsync(user, roleName);
        }
    }
}
