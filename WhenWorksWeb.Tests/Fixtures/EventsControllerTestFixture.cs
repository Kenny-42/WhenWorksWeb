using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using WhenWorksWeb.Controllers;
using WhenWorksWeb.Hubs;
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
    /// NSubstitute-substituted hub context (see the "avoid Moq" convention) shared across every
    /// <see cref="CreateController"/> call within a test — spinning up a real SignalR client isn't
    /// practical at this test tier, so live-sync tests assert on the calls made to
    /// <see cref="HubClientProxy"/>/<see cref="HubClients"/> instead (method name, group, payload
    /// shape), not on transport behavior.
    /// </summary>
    protected IHubContext<EventHub> Hub { get; }

    /// <summary>
    /// The substituted <see cref="IHubClients"/> behind <see cref="Hub"/>'s <c>Clients</c>
    /// property — exposed so a test can assert exactly which group <c>Group(...)</c>/
    /// <c>GroupExcept(...)</c> was called with (e.g. <c>EventHub.GroupName(code)</c>).
    /// </summary>
    protected IHubClients HubClients { get; }

    /// <summary>
    /// The single <see cref="IClientProxy"/> both <see cref="HubClients"/>' <c>Group(...)</c> and
    /// <c>GroupExcept(...)</c> are stubbed to return — assert on this to verify a broadcast's
    /// method name and payload (e.g. <c>HubClientProxy.Received(1).SendAsync("AvailabilityChanged", ...)</c>).
    /// </summary>
    protected IClientProxy HubClientProxy { get; }

    protected EventsControllerTestFixture()
    {
        Hub = Substitute.For<IHubContext<EventHub>>();
        HubClients = Substitute.For<IHubClients>();
        HubClientProxy = Substitute.For<IClientProxy>();

        HubClients.Group(Arg.Any<string>()).Returns(HubClientProxy);
        HubClients.GroupExcept(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns(HubClientProxy);
        Hub.Clients.Returns(HubClients);
    }

    /// <summary>
    /// Builds a real <see cref="EventsController"/> with a real <see cref="UniqueCodeGenerator"/> and
    /// <see cref="UserManager{TUser}"/> (both backed by <see cref="SqliteDbContextFixture.Db"/>), a real
    /// ephemeral <see cref="IDataProtectionProvider"/> (the officially supported non-persisted provider for
    /// exactly this kind of test/short-lived scenario — not a mock), the substituted <see cref="Hub"/>, and a
    /// real <see cref="DefaultHttpContext"/> attached so cookie/user/URL access work.
    /// </summary>
    protected (EventsController Controller, DefaultHttpContext HttpContext) CreateController(
        ApplicationUser? user = null,
        IReadOnlyDictionary<string, string>? requestCookies = null)
    {
        var controller = new EventsController(
            Db,
            new UniqueCodeGenerator(Db),
            new EventDateCleanupService(Db),
            Hub,
            TestUserManagerFactory.Create(Db),
            _dataProtectionProvider);

        var httpContext = ControllerTestContext.AttachContext(controller, user, requestCookies);

        return (controller, httpContext);
    }
}
