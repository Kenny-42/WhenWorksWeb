using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Areas.Admin.Pages.Users;

[Authorize(Roles = "Admin")]
public class ManageUsersModel(UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public string? Email { get; set; }

    public string? StatusMessage { get; set; }

    public IList<ApplicationUser> AdminUsers { get; set; } = [];

    public async Task OnGetAsync()
    {
        AdminUsers = await userManager.GetUsersInRoleAsync("Admin");
    }

    public async Task<IActionResult> OnPostAddAdminAsync()
    {
        var currentEmail = await GetCurrentUserEmailAsync();
        if (currentEmail is null)
        {
            return Challenge();
        }

        // Only your account can promote admins
        if (!string.Equals(currentEmail, "kenny@mail.com", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = $"Not authorized. Signed in as: {currentEmail ?? "(no email)"}";
            await LoadAdminsAsync();
            return Page();
        }

        Email = Email?.Trim();

        if (string.IsNullOrWhiteSpace(Email))
        {
            StatusMessage = "Email is required.";
            await LoadAdminsAsync();
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Email);
        if (user == null)
        {
            StatusMessage = "User not found.";
            await LoadAdminsAsync();
            return Page();
        }

        if (await userManager.IsInRoleAsync(user, "Admin"))
        {
            StatusMessage = "User is already an admin.";
            await LoadAdminsAsync();
            return Page();
        }

        var result = await userManager.AddToRoleAsync(user, "Admin");

        StatusMessage = result.Succeeded
            ? $"User {Email} was added as an admin."
            : string.Join("; ", result.Errors.Select(e => e.Description));

        await LoadAdminsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAdminAsync(string? email)
    {
        var currentEmail = await GetCurrentUserEmailAsync();
        if (currentEmail is null)
        {
            return Challenge();
        }

        if (!string.Equals(currentEmail, "kenny@mail.com", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = $"Not authorized. Signed in as: {currentEmail}";
            await LoadAdminsAsync();
            return Page();
        }

        email = email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            StatusMessage = "Email is required.";
            await LoadAdminsAsync();
            return Page();
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            StatusMessage = "User not found.";
            await LoadAdminsAsync();
            return Page();
        }

        if (!await userManager.IsInRoleAsync(user, "Admin"))
        {
            StatusMessage = "User is not an admin.";
            await LoadAdminsAsync();
            return Page();
        }

        if (string.Equals(user.Email, "kenny@mail.com", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "You cannot remove admin access from your protected account.";
            await LoadAdminsAsync();
            return Page();
        }

        var result = await userManager.RemoveFromRoleAsync(user, "Admin");
        StatusMessage = result.Succeeded
            ? $"User {email} was removed from admin."
            : string.Join("; ", result.Errors.Select(e => e.Description));

        await LoadAdminsAsync();
        return Page();
    }

    private async Task<string?> GetCurrentUserEmailAsync()
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return null;
        }

        return await userManager.GetEmailAsync(currentUser);
    }

    private async Task LoadAdminsAsync()
    {
        AdminUsers = await userManager.GetUsersInRoleAsync("Admin");
    }
}
