using Microsoft.AspNetCore.Mvc.RazorPages;
using WhenWorksWeb.Areas.Identity.Pages.Account;
using WhenWorksWeb.Tests.Fixtures;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account;

/// <summary>
/// Guards against <see cref="RegisterConfirmationModel"/> acting as an unauthenticated
/// account-existence oracle: <c>OnGet</c> must render the same way regardless of whether the
/// submitted email belongs to a real account, matching <c>ResendEmailConfirmation.cshtml.cs</c>'s
/// identical-response-regardless-of-existence pattern. See
/// Spec/Bugs/BUGS-email-verification-review-findings.ospec.
/// </summary>
public class RegisterConfirmationTests
{
    private static RegisterConfirmationModel CreateModel()
    {
        var model = new RegisterConfirmationModel();
        PageModelTestContext.AttachContext(model);
        return model;
    }

    /// <summary>
    /// The email address doesn't need to belong to any real account -- OnGet performs no user-store
    /// lookup at all, so there's nothing to distinguish an existing account from a nonexistent one.
    /// </summary>
    [Fact]
    public void OnGet_WithAnyEmail_RendersPageAndEchoesEmail()
    {
        var model = CreateModel();

        var result = model.OnGet("nonexistent-address@example.com");

        Assert.IsType<PageResult>(result);
        Assert.Equal("nonexistent-address@example.com", model.Email);
    }

    /// <summary>
    /// Pins that OnGet never returns a status distinguishing an existing account (previously 200)
    /// from a nonexistent one (previously 404) -- both must produce the same <see cref="PageResult"/>.
    /// </summary>
    [Fact]
    public void OnGet_WithDifferentEmails_AlwaysReturnsPageResult()
    {
        var existingLikeEmail = CreateModel().OnGet("existing@example.com");
        var nonExistentLikeEmail = CreateModel().OnGet("nonexistent@example.com");

        Assert.IsType<PageResult>(existingLikeEmail);
        Assert.IsType<PageResult>(nonExistentLikeEmail);
    }

    [Fact]
    public void OnGet_WithNullEmail_RendersErrorState()
    {
        var model = CreateModel();

        var result = model.OnGet(null);

        Assert.IsType<PageResult>(result);
        Assert.True(model.HasError);
    }

    [Fact]
    public void OnGet_WithEmptyEmail_RendersErrorState()
    {
        var model = CreateModel();

        var result = model.OnGet(string.Empty);

        Assert.IsType<PageResult>(result);
        Assert.True(model.HasError);
    }
}
