using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Models;

/// <summary>
/// Tier 2 tests for <see cref="WhenWorksWeb.Models.IdentityConfiguration.Configure"/>'s server-side
/// password policy -- the actual enforcement point behind the client-facing
/// <c>ModelConstants.PasswordMinLength</c>/<c>PasswordComplexityPattern</c> rules exercised by
/// <c>ChangePasswordModelInputTests</c>/<c>SetPasswordModelInputTests</c>/<c>RegisterModelInputTests</c>.
/// Those tests only cover the page-level <c>[RegularExpression]</c> attributes; nothing else in the
/// suite calls <c>IdentityOptions.Password</c> at all, so a regression here (e.g.
/// <c>RequireNonAlphanumeric</c> silently reverting to <see langword="false"/>) would otherwise go
/// undetected. Run against a real <see cref="Microsoft.AspNetCore.Identity.UserManager{TUser}"/> via
/// <see cref="TestUserManagerFactory"/>, per this project's "test through the real thing" convention
/// -- not a re-implementation of Identity's password validators.
/// </summary>
public class IdentityConfigurationTests : SqliteDbContextFixture
{
    [Theory]
    [InlineData("short1!")] // below the 8-character minimum
    [InlineData("alllowercase1!")] // no uppercase
    [InlineData("ALLUPPERCASE1!")] // no lowercase
    [InlineData("NoDigitsHere!")] // no digit
    [InlineData("NoSymbolsHere1")] // no non-alphanumeric
    public async Task CreateAsync_WithPasswordViolatingPolicy_Fails(string password)
    {
        var userManager = TestUserManagerFactory.Create(Db);
        var user = new ApplicationUserBuilder().WithUserName("weakpassworduser").WithEmail("weak@example.com").Build();

        var result = await userManager.CreateAsync(user, password);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateAsync_WithPasswordMeetingPolicy_Succeeds()
    {
        var userManager = TestUserManagerFactory.Create(Db);
        var user = new ApplicationUserBuilder().WithUserName("strongpassworduser").WithEmail("strong@example.com").Build();

        var result = await userManager.CreateAsync(user, "Str0ng!Pass");

        Assert.True(result.Succeeded);
    }
}
