using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WhenWorksWeb.Areas.Identity.Pages.Account.Manage;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Tier 2 tests for <see cref="DownloadPersonalDataModel.OnPostAsync"/>, added by
/// Spec/Bugs/BUGS-missing-download-personal-data-page.ospec (Issue #107). Against a real
/// <c>UserManager&lt;ApplicationUser&gt;</c> backed by SQLite (<see cref="TestUserManagerFactory"/>),
/// per this project's "test through the real thing" convention -- not a re-implementation of the
/// reflection/serialization the page performs.
/// </summary>
public class DownloadPersonalDataModelTests : SqliteDbContextFixture
{
    [Fact]
    public async Task OnPostAsync_WithAnonymousUser_ReturnsNotFound()
    {
        var userManager = TestUserManagerFactory.Create(Db);
        var pageModel = new DownloadPersonalDataModel(userManager, NullLogger<DownloadPersonalDataModel>.Instance);
        PageModelTestContext.AttachContext(pageModel, user: null);

        var result = await pageModel.OnPostAsync();

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_WithSignedInUser_ReturnsJsonFileContainingPersonalDataProperties()
    {
        var userManager = TestUserManagerFactory.Create(Db);
        var user = new ApplicationUserBuilder()
            .WithUserName("downloaduser")
            .WithEmail("download@example.com")
            .WithDisplayName("Download Me")
            .WithColor("ff66c4")
            .Build();
        await userManager.CreateAsync(user);

        var pageModel = new DownloadPersonalDataModel(userManager, NullLogger<DownloadPersonalDataModel>.Instance);
        PageModelTestContext.AttachContext(pageModel, user);

        var result = await pageModel.OnPostAsync();

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json", fileResult.ContentType);
        Assert.Equal("attachment; filename=PersonalData.json", pageModel.HttpContext.Response.Headers["Content-Disposition"].ToString());

        var personalData = JsonSerializer.Deserialize<Dictionary<string, string>>(fileResult.FileContents)!;

        // Base IdentityUser fields carrying [PersonalData]/[ProtectedPersonalData].
        Assert.Equal(user.Id, personalData["Id"]);
        Assert.Equal("downloaduser", personalData["UserName"]);
        Assert.Equal("download@example.com", personalData["Email"]);

        // ApplicationUser's own [PersonalData]-attributed fields.
        Assert.Equal("Download Me", personalData["DisplayName"]);
        Assert.Equal("ff66c4", personalData["Color"]);
        Assert.Contains("CreatedAt", personalData.Keys);
        Assert.Contains("LastActiveAt", personalData.Keys);

        // No authenticator set up and no external logins linked -- still exported per stock
        // Identity behavior (the authenticator key entry is present but null; no external-login
        // entries at all, since GetLoginsAsync returns an empty list to loop over).
        Assert.True(personalData.ContainsKey("Authenticator Key"));
        Assert.Null(personalData["Authenticator Key"]);
        Assert.DoesNotContain(personalData.Keys, k => k.EndsWith("external login provider key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnPostAsync_WithNonPersonalDataProperty_DoesNotIncludeItInTheExport()
    {
        var userManager = TestUserManagerFactory.Create(Db);
        var user = new ApplicationUserBuilder().WithUserName("secretuser").Build();
        await userManager.CreateAsync(user);

        var pageModel = new DownloadPersonalDataModel(userManager, NullLogger<DownloadPersonalDataModel>.Instance);
        PageModelTestContext.AttachContext(pageModel, user);

        var result = await pageModel.OnPostAsync();

        var fileResult = Assert.IsType<FileContentResult>(result);
        var personalData = JsonSerializer.Deserialize<Dictionary<string, string>>(fileResult.FileContents)!;

        // PasswordHash/SecurityStamp/ConcurrencyStamp are not [PersonalData]-attributed on
        // IdentityUser -- confirms the export reflects over the attribute rather than every property.
        Assert.DoesNotContain("PasswordHash", personalData.Keys);
        Assert.DoesNotContain("SecurityStamp", personalData.Keys);
        Assert.DoesNotContain("ConcurrencyStamp", personalData.Keys);
    }
}
