using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhenWorksWeb.Areas.Identity.Pages.Account.Manage;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Tier 2 tests for <see cref="EmailModel.OnPostChangeEmailAsync"/>'s duplicate-email detection,
/// added by Spec/Features/FEATURES-tighten-account-validation.ospec (Issue #81). Against a real
/// <c>UserManager&lt;ApplicationUser&gt;</c> backed by SQLite (<see cref="TestUserManagerFactory"/>),
/// per this project's "test through the real thing" convention -- not a re-implementation of
/// <c>FindByEmailAsync</c>'s lookup.
/// </summary>
/// <remarks>
/// <see cref="EmailModel"/>'s <c>SignInManager</c>/<c>IEmailSender</c> dependencies are passed
/// <see langword="null"/>! here because the scenarios covered (a rejected duplicate, and the
/// "unchanged" no-op) never reach the branch that sends a confirmation email or calls
/// <c>Url.Page</c> -- only the successful email-change branch does, and that branch is unchanged
/// scaffolding out of scope for this spec (see its Scope section).
/// </remarks>
public class EmailModelTests : SqliteDbContextFixture
{
    [Fact]
    public async Task OnPostChangeEmailAsync_WithEmailAlreadyRegisteredToAnotherAccount_AddsModelErrorAndLeavesEmailUnchanged()
    {
        var userManager = TestUserManagerFactory.Create(Db);

        var otherUser = new ApplicationUserBuilder().WithUserName("otheruser").WithEmail("taken@example.com").Build();
        await userManager.CreateAsync(otherUser);

        var currentUser = new ApplicationUserBuilder().WithUserName("currentuser").WithEmail("current@example.com").Build();
        await userManager.CreateAsync(currentUser);

        var pageModel = new EmailModel(userManager, null!, null!)
        {
            Input = new EmailModel.InputModel { NewEmail = "taken@example.com" }
        };
        PageModelTestContext.AttachContext(pageModel, currentUser);

        var result = await pageModel.OnPostChangeEmailAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Contains(
            pageModel.ModelState[string.Empty]!.Errors,
            e => e.ErrorMessage.Contains("already registered", StringComparison.OrdinalIgnoreCase));

        var reloadedCurrentUser = await userManager.FindByIdAsync(currentUser.Id);
        Assert.Equal("current@example.com", reloadedCurrentUser!.Email);
    }

    [Fact]
    public async Task OnPostChangeEmailAsync_WithOwnCurrentEmailUnchanged_DoesNotTreatItAsADuplicate()
    {
        var userManager = TestUserManagerFactory.Create(Db);

        var currentUser = new ApplicationUserBuilder().WithUserName("currentuser").WithEmail("current@example.com").Build();
        await userManager.CreateAsync(currentUser);

        var pageModel = new EmailModel(userManager, null!, null!)
        {
            Input = new EmailModel.InputModel { NewEmail = "current@example.com" }
        };
        PageModelTestContext.AttachContext(pageModel, currentUser);

        var result = await pageModel.OnPostChangeEmailAsync();

        // Falls into the pre-existing "unchanged" branch (Input.NewEmail == current email),
        // not the new duplicate-email branch -- a user resubmitting their own current email is
        // not "someone else's" registered email.
        Assert.IsType<RedirectToPageResult>(result);
        Assert.True(pageModel.ModelState.IsValid);
        Assert.Equal("Your email is unchanged.", pageModel.StatusMessage);
    }
}
