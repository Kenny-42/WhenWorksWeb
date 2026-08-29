using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using WhenWorksWeb.Controllers;
using WhenWorksWeb.Models;
using WhenWorksWeb.Services;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Shared base for the <see cref="EventsController"/> test classes (split across
/// <c>EventsControllerTests</c>, <c>EventsControllerSignInTests</c>, <c>EventsControllerHomeTests</c>, mirroring
/// the controller's own partial-class split). Provides a real, fully wired <see cref="EventsController"/>
/// backed by the SQLite fixture.
/// </summary>
public abstract class EventsControllerTestFixture : SqliteDbContextFixture
{
    // One provider per test method (this class is re-instantiated per test by xUnit), shared across every
    // CreateController() call within that test — mirroring the single app-wide provider in production. A
    // cookie protected by one controller instance's protector must be unprotectable by another's within the
    // same test (e.g. a sign-in POST's cookie being read back by a later Home GET), which requires reusing the
    // same key material; a fresh EphemeralDataProtectionProvider per call would break that round trip.
    private readonly IDataProtectionProvider _dataProtectionProvider = new EphemeralDataProtectionProvider();

    /// <summary>
    /// Builds a real <see cref="EventsController"/> with a real <see cref="UniqueCodeGenerator"/> and
    /// <see cref="UserManager{TUser}"/> (both backed by <see cref="SqliteDbContextFixture.Db"/>), a real
    /// ephemeral <see cref="IDataProtectionProvider"/> (the officially supported non-persisted provider for
    /// exactly this kind of test/short-lived scenario — not a mock), and a real <see cref="DefaultHttpContext"/>
    /// attached so cookie/user/URL access work.
    /// </summary>
    protected (EventsController Controller, DefaultHttpContext HttpContext) CreateController(
        ApplicationUser? user = null,
        IReadOnlyDictionary<string, string>? requestCookies = null)
    {
        var controller = new EventsController(
            Db,
            new UniqueCodeGenerator(Db),
            new EventDateCleanupService(Db),
            TestUserManagerFactory.Create(Db),
            _dataProtectionProvider);

        var httpContext = ControllerTestContext.AttachContext(controller, user, requestCookies);

        return (controller, httpContext);
    }
}
