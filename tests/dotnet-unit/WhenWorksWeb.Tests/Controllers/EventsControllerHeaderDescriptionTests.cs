using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Controllers;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Controllers;

/// <summary>
/// Tests for the shared page header's description card (<c>EventHeaderViewModel.Description</c>,
/// rendered by <c>_EventHeader.cshtml</c>) across the Home/People/Finalize/Settings tabs — see
/// <c>EventsController.GetEventDescriptionAsync</c>/<c>ResolveHeaderDescription</c> in
/// <c>EventsController.EventPage.cs</c> for the null/empty-string resolution this covers.
/// </summary>
public class EventsControllerHeaderDescriptionTests : EventsControllerTestFixture
{
    private async Task<Event> CreateEventAsync(string code = "BCDFGH")
    {
        var evt = new EventBuilder().WithCode(code).Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();
        return evt;
    }

    /// <summary>
    /// Signs a new participant into an already-created event via a real SignIn round trip,
    /// returning a controller instance already carrying that participant's real access cookie.
    /// </summary>
    private async Task<EventsController> SignInParticipantAsync(Event evt, string displayName = "Alice", string color = "ff66c4")
    {
        var code = evt.Code;

        var (signInController, signInHttpContext) = CreateController();
        await signInController.SignIn(code, new EventSignInViewModel { Code = code, DisplayName = displayName, Color = color }, CancellationToken.None);

        var cookieValue = ControllerTestContext.GetResponseCookieValue(signInHttpContext, $"WhenWorksWeb.EventAccess.{code}");
        Assert.NotNull(cookieValue);

        var (controller, _) = CreateController(
            requestCookies: new Dictionary<string, string> { [$"WhenWorksWeb.EventAccess.{code}"] = cookieValue! });

        return controller;
    }

    // ---- Home tab ----

    [Fact]
    public async Task Home_WithNoEventSettingsRow_HeaderDescriptionIsDefaultText()
    {
        var evt = await CreateEventAsync();
        var controller = await SignInParticipantAsync(evt);

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventHomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(WhenWorksWeb.Common.ModelConstants.DefaultEventDescription, model.Header.Description);
    }

    [Fact]
    public async Task Home_WithSettingsRowAndNullDescription_HeaderDescriptionIsDefaultText()
    {
        var evt = await CreateEventAsync();
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎉", Description = null });
        await Db.SaveChangesAsync();
        var controller = await SignInParticipantAsync(evt);

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventHomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(WhenWorksWeb.Common.ModelConstants.DefaultEventDescription, model.Header.Description);
    }

    /// <summary>
    /// An empty-string Description (an organizer explicitly cleared a previously-set description
    /// — see UpdateDetails) means the card should be hidden entirely, not fall back to the
    /// default text like a never-customized (null) description does.
    /// </summary>
    [Fact]
    public async Task Home_WithEmptyStringDescription_HeaderDescriptionIsNull()
    {
        var evt = await CreateEventAsync();
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎉", Description = string.Empty });
        await Db.SaveChangesAsync();
        var controller = await SignInParticipantAsync(evt);

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventHomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Null(model.Header.Description);
    }

    [Fact]
    public async Task Home_WithCustomDescription_HeaderDescriptionIsThatText()
    {
        var evt = await CreateEventAsync();
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎉", Description = "Bring snacks." });
        await Db.SaveChangesAsync();
        var controller = await SignInParticipantAsync(evt);

        var result = await controller.Home("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventHomeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Bring snacks.", model.Header.Description);
    }

    // ---- People tab ----

    [Fact]
    public async Task People_WithCustomDescription_HeaderDescriptionIsThatText()
    {
        var evt = await CreateEventAsync();
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎉", Description = "Bring snacks." });
        await Db.SaveChangesAsync();
        var controller = await SignInParticipantAsync(evt);

        var result = await controller.People("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventPeopleViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Bring snacks.", model.Header.Description);
    }

    [Fact]
    public async Task People_WithExplicitlyClearedDescription_HeaderDescriptionIsNull()
    {
        var evt = await CreateEventAsync();
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎉", Description = string.Empty });
        await Db.SaveChangesAsync();
        var controller = await SignInParticipantAsync(evt);

        var result = await controller.People("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventPeopleViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Null(model.Header.Description);
    }

    // ---- Finalize tab ----

    [Fact]
    public async Task Finalize_WithCustomDescription_HeaderDescriptionIsThatText()
    {
        var evt = await CreateEventAsync();
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎉", Description = "Bring snacks." });
        await Db.SaveChangesAsync();
        var controller = await SignInParticipantAsync(evt);

        var result = await controller.Finalize("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventFinalizeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Bring snacks.", model.Header.Description);
    }

    [Fact]
    public async Task Finalize_WithNoEventSettingsRow_HeaderDescriptionIsDefaultText()
    {
        var evt = await CreateEventAsync();
        var controller = await SignInParticipantAsync(evt);

        var result = await controller.Finalize("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventFinalizeViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(WhenWorksWeb.Common.ModelConstants.DefaultEventDescription, model.Header.Description);
    }

    // ---- Settings tab ----
    // Settings resolves the header description from its own already-loaded EventSettings row
    // (ResolveHeaderDescription) rather than GetEventDescriptionAsync's separate query — covered
    // independently since it's a distinct code path to the one exercised above.

    [Fact]
    public async Task Settings_WithNoEventSettingsRow_HeaderDescriptionIsDefaultText()
    {
        var evt = await CreateEventAsync();
        var controller = await SignInParticipantAsync(evt);

        var result = await controller.Settings("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventSettingsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(WhenWorksWeb.Common.ModelConstants.DefaultEventDescription, model.Header.Description);
    }

    [Fact]
    public async Task Settings_WithExplicitlyClearedDescription_HeaderDescriptionIsNull()
    {
        var evt = await CreateEventAsync();
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎉", Description = string.Empty });
        await Db.SaveChangesAsync();
        var controller = await SignInParticipantAsync(evt);

        var result = await controller.Settings("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventSettingsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Null(model.Header.Description);
    }

    [Fact]
    public async Task Settings_WithCustomDescription_HeaderDescriptionIsThatText()
    {
        var evt = await CreateEventAsync();
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎉", Description = "Bring snacks." });
        await Db.SaveChangesAsync();
        var controller = await SignInParticipantAsync(evt);

        var result = await controller.Settings("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventSettingsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Bring snacks.", model.Header.Description);
    }
}
