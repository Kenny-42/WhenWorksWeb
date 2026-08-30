using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhenWorksWeb.Common;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Areas.Admin.Pages.Users;

/// <summary>
/// Admin-only page for granting and revoking the "Admin" role. Only the hardcoded root admin account
/// (kenny@mail.com) is allowed to promote or demote other accounts, and that account can never be demoted.
/// </summary>
/// <param name="userManager">Resolves and modifies role membership for application users.</param>
[Authorize(Roles = "Admin")]
public class ManageUsersModel(UserManager<ApplicationUser> userManager) : PageModel
{
    /// <summary>
    /// The email address entered in the "add admin" form.
    /// </summary>
    [BindProperty]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(ModelConstants.UserEmailMaxLength, ErrorMessage = "Email must be {1} characters or fewer.")]
    public string? Email { get; set; }

    /// <summary>
    /// A status or error message to display after a form submission.
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// The users currently in the "Admin" role, for display in the admin list.
    /// </summary>
    public IList<ApplicationUser> AdminUsers { get; set; } = [];

    /// <summary>
    /// Loads the current list of admin users for the initial page render.
    /// </summary>
    public async Task OnGetAsync()
    {
        AdminUsers = await userManager.GetUsersInRoleAsync("Admin");
    }

    /// <summary>
    /// Grants the "Admin" role to the user with the submitted <see cref="Email"/>, if the current
    /// user is the protected root admin account.
    /// </summary>
    /// <returns>The page, redisplayed with a status message describing the result.</returns>
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

        // Trim before validating, same as the rest of the site, so e.g. an all-whitespace value
        // fails [Required] rather than being accepted and only then trimmed down to an empty
        // string. Re-runs validation against the trimmed value since the framework's own
        // model-binding validation already ran once against the untrimmed input.
        //
        // Deliberately re-validates just the Email property (Validator.TryValidateProperty)
        // rather than calling TryValidateModel(this) on the whole page model -- unlike a
        // dedicated view model (e.g. EventUpdateDetailsViewModel), "this" is the PageModel
        // itself, and ASP.NET Core's ObjectModelValidator walks every public property reachable
        // from it, including base-class ones like HttpContext/Request that aren't meant to be
        // validated at all.
        Email = Email?.Trim();

        // Razor Pages' own model-binding validation already ran once, against the raw untrimmed
        // Email, before this handler started -- e.g. a value with incidental leading/trailing
        // whitespace that pushes it past UserEmailMaxLength fails that automatic pass and leaves
        // a stale error under the "Email" ModelState key even though the trimmed value below is
        // perfectly valid. Clear it before re-validating, or a legitimate trimmed email would
        // still render with a (stale, incorrect) validation error next to the success message.
        ModelState.ClearValidationState(nameof(Email));

        var validationContext = new ValidationContext(this) { MemberName = nameof(Email) };
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateProperty(Email, validationContext, validationResults))
        {
            foreach (var validationResult in validationResults)
            {
                ModelState.AddModelError(nameof(Email), validationResult.ErrorMessage ?? "Enter a valid email address.");
            }

            await LoadAdminsAsync();
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Email!);
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

    /// <summary>
    /// Revokes the "Admin" role from the given user, if the current user is the protected root admin
    /// account and the target is not that same protected account.
    /// </summary>
    /// <param name="email">The email address of the admin to demote.</param>
    /// <returns>The page, redisplayed with a status message describing the result.</returns>
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

    /// <summary>
    /// Returns the email address of the currently signed-in user, or null if none is signed in.
    /// </summary>
    private async Task<string?> GetCurrentUserEmailAsync()
    {
        var currentUser = await userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return null;
        }

        return await userManager.GetEmailAsync(currentUser);
    }

    /// <summary>
    /// Refreshes <see cref="AdminUsers"/> from the current role membership.
    /// </summary>
    private async Task LoadAdminsAsync()
    {
        AdminUsers = await userManager.GetUsersInRoleAsync("Admin");
    }
}
