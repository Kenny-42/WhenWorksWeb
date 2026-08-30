using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WhenWorksWeb.Areas.Admin.Pages.Users;
using WhenWorksWeb.Common;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Areas.Admin.Pages.Users;

/// <summary>
/// Tests for <see cref="ManageUsersModel"/>'s "Add Admin" form, added by
/// Spec/Features/FEATURES-tighten-input-validation-site-wide.ospec's Section 4 (Issue #88). Two
/// groups: Tier 1 unit tests of <see cref="ManageUsersModel.Email"/>'s validation attributes via
/// <see cref="Validator.TryValidateProperty"/> (see CODING_CONVENTIONS.md's Testing Conventions
/// for why -- matches <c>EventUpdateDetailsViewModelTests</c>/<c>IndexModelInputTests</c>), and
/// Tier 2 behavior tests of <see cref="ManageUsersModel.OnPostAddAdminAsync"/> against a real
/// <c>UserManager&lt;ApplicationUser&gt;</c> (<see cref="TestUserManagerFactory"/>), matching
/// <c>EmailModelTests</c>' pattern for a Razor <see cref="PageModel"/>.
/// </summary>
public class ManageUsersModelTests : SqliteDbContextFixture
{
    private const string RootAdminEmail = "kenny@mail.com";

    // ---- Email attribute validation (Tier 1) ----

    private static bool IsEmailValid(string? candidate)
    {
        var model = new ManageUsersModel(null!);
        var context = new ValidationContext(model) { MemberName = nameof(ManageUsersModel.Email) };
        var results = new List<ValidationResult>();

        return Validator.TryValidateProperty(candidate, context, results);
    }

    [Theory]
    [InlineData(null)] // missing (Required)
    [InlineData("")] // empty
    [InlineData("   ")] // whitespace-only -- EmailAddress' shape check rejects this too, no @ present
    public void Email_RejectsMissingOrEmptyValue(string? email)
    {
        Assert.False(IsEmailValid(email));
    }

    // [EmailAddress]'s built-in check (System.ComponentModel.DataAnnotations.EmailAddressAttribute)
    // is deliberately minimal -- exactly one '@', not in the first or last position -- rather than a
    // full RFC-shape validator, so these cases are chosen to match what that specific check actually
    // rejects rather than a stricter assumption of "valid email shape."
    [Theory]
    [InlineData("notanemail")] // no '@' at all
    [InlineData("missing-domain@")] // '@' is the last character
    [InlineData("@missing-local.com")] // '@' is the first character
    [InlineData("two@at@signs.com")] // more than one '@'
    public void Email_RejectsMalformedShapes(string email)
    {
        Assert.False(IsEmailValid(email));
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("first.last+tag@sub.example.co.uk")]
    [InlineData("kenny@mail.com")]
    public void Email_AcceptsWellFormedAddresses(string email)
    {
        Assert.True(IsEmailValid(email));
    }

    [Fact]
    public void Email_RejectsValueOverMaxLength()
    {
        // A syntactically valid address that is still one character past the StringLength bound --
        // long local part keeps [EmailAddress]'s shape check happy so only the length check fails.
        var localPart = new string('a', ModelConstants.UserEmailMaxLength - "@example.com".Length + 1);
        var tooLong = $"{localPart}@example.com";

        Assert.Equal(ModelConstants.UserEmailMaxLength + 1, tooLong.Length);
        Assert.False(IsEmailValid(tooLong));
    }

    [Fact]
    public void Email_AcceptsValueAtMaxLength()
    {
        var localPart = new string('a', ModelConstants.UserEmailMaxLength - "@example.com".Length);
        var atMax = $"{localPart}@example.com";

        Assert.Equal(ModelConstants.UserEmailMaxLength, atMax.Length);
        Assert.True(IsEmailValid(atMax));
    }

    // ---- OnPostAddAdminAsync behavior (Tier 2) ----

    private async Task<(WhenWorksWeb.Models.ApplicationUser RootAdmin, ManageUsersModel PageModel)> CreateSignedInRootAdminAsync()
    {
        var userManager = TestUserManagerFactory.Create(Db);
        var rootAdmin = new ApplicationUserBuilder().WithUserName("kenny").WithEmail(RootAdminEmail).Build();
        var createResult = await userManager.CreateAsync(rootAdmin);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(e => e.Description)));

        var pageModel = new ManageUsersModel(userManager);
        PageModelTestContext.AttachContext(pageModel, rootAdmin);

        return (rootAdmin, pageModel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notanemail")]
    public async Task OnPostAddAdminAsync_WithInvalidEmail_ReturnsPageWithModelErrorAndDoesNotPromoteAnyone(string? invalidEmail)
    {
        var (_, pageModel) = await CreateSignedInRootAdminAsync();
        pageModel.Email = invalidEmail;

        var result = await pageModel.OnPostAddAdminAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.Null(pageModel.StatusMessage); // the old "User not found."/"Email is required." message
                                               // path is never reached -- TryValidateModel short-circuits first
    }

    [Fact]
    public async Task OnPostAddAdminAsync_WithEmailOverMaxLength_ReturnsPageWithModelError()
    {
        var (_, pageModel) = await CreateSignedInRootAdminAsync();
        var localPart = new string('a', ModelConstants.UserEmailMaxLength - "@example.com".Length + 1);
        pageModel.Email = $"{localPart}@example.com";

        var result = await pageModel.OnPostAddAdminAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostAddAdminAsync_WithWhitespacePaddedValidEmail_TrimsBeforeValidatingAndSucceeds()
    {
        var (_, pageModel) = await CreateSignedInRootAdminAsync();
        var userManager = TestUserManagerFactory.Create(Db);
        var target = new ApplicationUserBuilder().WithUserName("target").WithEmail("target@example.com").Build();
        await userManager.CreateAsync(target);

        // AddToRoleAsync requires the "Admin" role row to already exist -- unlike Program.cs, which
        // seeds it at startup (IdentityRoleSeeder), a Tier 2 UserManager has no role-seeding step, so
        // the test seeds it directly.
        Db.Roles.Add(new IdentityRole("Admin") { NormalizedName = "ADMIN" });
        await Db.SaveChangesAsync();

        pageModel.Email = "  target@example.com  ";

        var result = await pageModel.OnPostAddAdminAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ModelState.IsValid);
        Assert.Equal("target@example.com", pageModel.Email);
        Assert.Contains("was added as an admin", pageModel.StatusMessage);
    }

    [Fact]
    public async Task OnPostAddAdminAsync_WithValidButUnknownEmail_ReturnsPageWithUserNotFoundMessage()
    {
        var (_, pageModel) = await CreateSignedInRootAdminAsync();
        pageModel.Email = "nobody@example.com";

        var result = await pageModel.OnPostAddAdminAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ModelState.IsValid); // shape is valid -- this is a lookup failure, not a validation one
        Assert.Equal("User not found.", pageModel.StatusMessage);
    }

    [Fact]
    public async Task OnPostAddAdminAsync_WhenNotRootAdmin_RejectsBeforeValidatingEmail()
    {
        // A non-root admin submitting even a malformed email should get the authorization message,
        // not a validation error -- the authorization check runs first.
        // Doesn't actually assign the "Admin" role -- OnPostAddAdminAsync's authorization check is
        // purely against the hardcoded root-admin email, unrelated to role membership, and
        // TestUserManagerFactory's UserManager (unlike CustomWebApplicationFactory's) has no
        // RoleManager/seeded "Admin" role to assign into.
        var userManager = TestUserManagerFactory.Create(Db);
        var nonRootAdmin = new ApplicationUserBuilder().WithUserName("otheradmin").WithEmail("otheradmin@example.com").Build();
        await userManager.CreateAsync(nonRootAdmin);

        var pageModel = new ManageUsersModel(userManager) { Email = "notanemail" };
        PageModelTestContext.AttachContext(pageModel, nonRootAdmin);

        var result = await pageModel.OnPostAddAdminAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ModelState.IsValid); // never reached the validation check
        Assert.Contains("Not authorized", pageModel.StatusMessage);
    }
}
