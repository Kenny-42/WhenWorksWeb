using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Controllers;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Controllers;

/// <summary>
/// Tests for <see cref="WhenWorksWeb.Controllers.EventsController"/>'s Settings tab actions
/// (<c>EventsController.Settings.cs</c>): the GET view-model build, and the organizer-only
/// UpdateDetails/DeleteEvent POSTs.
/// </summary>
public class EventsControllerSettingsTests : EventsControllerTestFixture
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
    /// The event's creator cookie is never set by this helper, so — per the Organizer Permission
    /// Model — this participant is not auto-flagged IsOrganizer; tests that need an organizer set
    /// <see cref="Participant.IsOrganizer"/> on the returned participant explicitly.
    /// </summary>
    private async Task<(Participant Participant, EventsController Controller)> SignInParticipantAsync(
        Event evt, string displayName = "Alice", string color = "ff66c4")
    {
        var code = evt.Code;

        var (signInController, signInHttpContext) = CreateController();
        await signInController.SignIn(code, new EventSignInViewModel { Code = code, DisplayName = displayName, Color = color }, CancellationToken.None);

        var cookieValue = ControllerTestContext.GetResponseCookieValue(signInHttpContext, $"WhenWorksWeb.EventAccess.{code}");
        Assert.NotNull(cookieValue);

        var (controller, _) = CreateController(
            requestCookies: new Dictionary<string, string> { [$"WhenWorksWeb.EventAccess.{code}"] = cookieValue! });

        var participant = await Db.Participants.SingleAsync(p => p.EventId == evt.Id && p.DisplayName == displayName);

        return (participant, controller);
    }

    /// <summary>Convenience wrapper for the common case: one event, one participant.</summary>
    private async Task<(Event Event, Participant Participant, EventsController Controller)> CreateEventWithSignedInParticipantAsync(
        string code = "BCDFGH", string displayName = "Alice", string color = "ff66c4")
    {
        var evt = await CreateEventAsync(code);
        var (participant, controller) = await SignInParticipantAsync(evt, displayName, color);
        return (evt, participant, controller);
    }

    // ---- Settings (GET) ----

    [Fact]
    public async Task Settings_WithNonExistentEventCode_ReturnsEventNotFoundView()
    {
        var (controller, _) = CreateController();

        var result = await controller.Settings("ZZZZZZ", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Shared/Error.cshtml", viewResult.ViewName);
    }

    [Fact]
    public async Task Settings_WithNoAccessCookie_RedirectsToSignIn()
    {
        await CreateEventAsync();
        var (controller, _) = CreateController();

        var result = await controller.Settings("BCDFGH", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventSignIn", redirect.RouteName);
    }

    [Fact]
    public async Task Settings_WithSoleParticipantAndNoOrganizer_FallsOpenAndCanManageEventIsTrue()
    {
        // The event has zero IsOrganizer participants — the fallback-open rule in
        // CanManageEventAsync should let this lone participant manage it.
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await controller.Settings("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventSettingsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.True(model.CanManageEvent);
    }

    [Fact]
    public async Task Settings_WithAnotherOrganizerPresent_NonOrganizerCanManageEventIsFalse()
    {
        var evt = await CreateEventAsync();
        var (organizer, _) = await SignInParticipantAsync(evt, "Organizer", "111111");
        organizer.IsOrganizer = true;
        await Db.SaveChangesAsync();

        var (_, controller) = await SignInParticipantAsync(evt, "Alice", "ff66c4");

        var result = await controller.Settings("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventSettingsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.CanManageEvent);
    }

    [Fact]
    public async Task Settings_ReturnsCurrentTitleDescriptionAndEmoji()
    {
        var evt = await CreateEventAsync();
        evt.Title = "Trivia Night";
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎲", Description = "Bring snacks." });
        await Db.SaveChangesAsync();

        var (_, controller) = await SignInParticipantAsync(evt);

        var result = await controller.Settings("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventSettingsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("Trivia Night", model.Title);
        Assert.Equal("Bring snacks.", model.Description);
        Assert.Equal("🎲", model.Emoji);
    }

    [Fact]
    public async Task Settings_WithNoEventSettingsRow_ReturnsDefaultEmojiAndNullDescription()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await controller.Settings("BCDFGH", CancellationToken.None);

        var model = Assert.IsType<EventSettingsViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(WhenWorksWeb.Common.ModelConstants.DefaultEventEmoji, model.Emoji);
        Assert.Null(model.Description);
    }

    // ---- UpdateDetails ----

    private static Task<IActionResult> UpdateDetailsAsync(EventsController controller, string code, string title, string? description, string? emoji)
        => controller.UpdateDetails(code, new EventUpdateDetailsViewModel { Title = title, Description = description, Emoji = emoji }, CancellationToken.None);

    [Fact]
    public async Task UpdateDetails_WithNonExistentEventCode_ReturnsEventNotFoundView()
    {
        var (controller, _) = CreateController();

        var result = await UpdateDetailsAsync(controller, "ZZZZZZ", "New Title", null, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Shared/Error.cshtml", viewResult.ViewName);
    }

    [Fact]
    public async Task UpdateDetails_WithNoAccessCookie_RedirectsToSignIn()
    {
        await CreateEventAsync();
        var (controller, _) = CreateController();

        var result = await UpdateDetailsAsync(controller, "BCDFGH", "New Title", null, null);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventSignIn", redirect.RouteName);
    }

    [Fact]
    public async Task UpdateDetails_WhenNotOrganizerAndAnotherOrganizerExists_ReturnsForbid()
    {
        var evt = await CreateEventAsync();
        var (organizer, _) = await SignInParticipantAsync(evt, "Organizer", "111111");
        organizer.IsOrganizer = true;
        await Db.SaveChangesAsync();

        var (_, controller) = await SignInParticipantAsync(evt, "Alice", "ff66c4");

        var result = await UpdateDetailsAsync(controller, "BCDFGH", "New Title", null, null);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("Test Event", (await Db.Events.SingleAsync()).Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateDetails_WithEmptyTitle_ReturnsSettingsViewWithModelErrorAndDoesNotSave(string emptyTitle)
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await UpdateDetailsAsync(controller, "BCDFGH", emptyTitle, "New description", "🎉");

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Settings", viewResult.ViewName);
        Assert.IsType<EventSettingsViewModel>(viewResult.Model);
        Assert.False(controller.ModelState.IsValid);

        Assert.Equal("Test Event", (await Db.Events.SingleAsync(e => e.Id == evt.Id)).Title);
    }

    [Fact]
    public async Task UpdateDetails_WithTitleOverMaxLength_ReturnsSettingsViewWithModelError()
    {
        var (_, _, controller) = await CreateEventWithSignedInParticipantAsync();
        var tooLong = new string('a', WhenWorksWeb.Common.ModelConstants.EventTitleMaxLength + 1);

        var result = await UpdateDetailsAsync(controller, "BCDFGH", tooLong, null, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Settings", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task UpdateDetails_WithDescriptionOverMaxLength_ReturnsSettingsViewWithModelErrorAndDoesNotSave()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        var tooLong = new string('a', WhenWorksWeb.Common.ModelConstants.EventDescriptionMaxLength + 1);

        var result = await UpdateDetailsAsync(controller, "BCDFGH", "Trivia Night", tooLong, null);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Settings", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(Db.EventSettings.Where(s => s.EventId == evt.Id));
    }

    [Theory]
    [InlineData("🎉🎉")]
    [InlineData("hi")]
    public async Task UpdateDetails_WithMultiCharacterEmoji_ReturnsSettingsViewWithModelErrorAndDoesNotSave(string invalidEmoji)
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await UpdateDetailsAsync(controller, "BCDFGH", "Trivia Night", null, invalidEmoji);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Settings", viewResult.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(Db.EventSettings.Where(s => s.EventId == evt.Id));
    }

    /// <summary>A whitespace-only emoji trims down to empty, which is "not provided" (valid,
    /// keeps the existing emoji), not a validation error — same as a whitespace-only description.</summary>
    [Fact]
    public async Task UpdateDetails_WithWhitespaceOnlyEmoji_TrimsToEmptyAndKeepsExistingEmoji()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎲", Description = null });
        await Db.SaveChangesAsync();

        var result = await UpdateDetailsAsync(controller, "BCDFGH", "Trivia Night", null, "  ");

        Assert.IsType<RedirectToRouteResult>(result);
        var settings = await Db.EventSettings.SingleAsync(s => s.EventId == evt.Id);
        Assert.Equal("🎲", settings.Emoji);
    }

    /// <summary>A single-codepoint emoji is the common case a picker actually produces.</summary>
    [Fact]
    public async Task UpdateDetails_WithSingleCodepointEmoji_Succeeds()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        await UpdateDetailsAsync(controller, "BCDFGH", "Trivia Night", null, "🎲");

        var settings = await Db.EventSettings.SingleAsync(s => s.EventId == evt.Id);
        Assert.Equal("🎲", settings.Emoji);
    }

    /// <summary>
    /// A multi-codepoint sequence (family: man, woman, girl, boy joined by ZWJ) is still one
    /// visible grapheme cluster and must be accepted, not just single-codepoint emoji.
    /// </summary>
    [Fact]
    public async Task UpdateDetails_WithMultiCodepointZwjSequenceEmoji_Succeeds()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        // Man + ZWJ + Woman + ZWJ + Girl + ZWJ + Boy, built via string.Concat with an
        // explicit \u200D (zero-width joiner) escape rather than a literal ZWJ embedded in
        // the source string, so the invisible joiner characters stay visible/greppable here.
        const string zwj = "\u200D";
        var family = string.Concat("\U0001F468", zwj, "\U0001F469", zwj, "\U0001F467", zwj, "\U0001F466");

        await UpdateDetailsAsync(controller, "BCDFGH", "Trivia Night", null, family);

        var settings = await Db.EventSettings.SingleAsync(s => s.EventId == evt.Id);
        Assert.Equal(family, settings.Emoji);
    }

    [Fact]
    public async Task UpdateDetails_WithValidData_UpdatesTitleDescriptionAndEmojiAndRedirects()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await UpdateDetailsAsync(controller, "BCDFGH", "  Trivia Night  ", "  Bring snacks.  ", "🎲");

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventSettings", redirect.RouteName);

        var updated = await Db.Events.SingleAsync(e => e.Id == evt.Id);
        Assert.Equal("Trivia Night", updated.Title);

        var settings = await Db.EventSettings.SingleAsync(s => s.EventId == evt.Id);
        Assert.Equal("Bring snacks.", settings.Description);
        Assert.Equal("🎲", settings.Emoji);
    }

    [Fact]
    public async Task UpdateDetails_WithBlankDescription_ClearsDescriptionToNull()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎉", Description = "Old description." });
        await Db.SaveChangesAsync();

        await UpdateDetailsAsync(controller, "BCDFGH", "Trivia Night", "   ", null);

        var settings = await Db.EventSettings.SingleAsync(s => s.EventId == evt.Id);
        Assert.Null(settings.Description);
    }

    [Fact]
    public async Task UpdateDetails_WithNoEmojiProvided_KeepsExistingEmojiUnchanged()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();
        Db.EventSettings.Add(new EventSettings { EventId = evt.Id, Emoji = "🎲", Description = null });
        await Db.SaveChangesAsync();

        await UpdateDetailsAsync(controller, "BCDFGH", "Trivia Night", null, null);

        var settings = await Db.EventSettings.SingleAsync(s => s.EventId == evt.Id);
        Assert.Equal("🎲", settings.Emoji);
    }

    [Fact]
    public async Task UpdateDetails_WhenNoOrganizerExistsYet_SoleParticipantCanStillUpdate()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await UpdateDetailsAsync(controller, "BCDFGH", "Trivia Night", null, null);

        Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("Trivia Night", (await Db.Events.SingleAsync(e => e.Id == evt.Id)).Title);
    }

    // ---- DeleteEvent ----

    [Fact]
    public async Task DeleteEvent_WithNonExistentEventCode_ReturnsEventNotFoundView()
    {
        var (controller, _) = CreateController();

        var result = await controller.DeleteEvent("ZZZZZZ", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Shared/Error.cshtml", viewResult.ViewName);
    }

    [Fact]
    public async Task DeleteEvent_WithNoAccessCookie_RedirectsToSignIn()
    {
        await CreateEventAsync();
        var (controller, _) = CreateController();

        var result = await controller.DeleteEvent("BCDFGH", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventSignIn", redirect.RouteName);
    }

    [Fact]
    public async Task DeleteEvent_WhenNotOrganizerAndAnotherOrganizerExists_ReturnsForbidAndDoesNotDelete()
    {
        var evt = await CreateEventAsync();
        var (organizer, _) = await SignInParticipantAsync(evt, "Organizer", "111111");
        organizer.IsOrganizer = true;
        await Db.SaveChangesAsync();

        var (_, controller) = await SignInParticipantAsync(evt, "Alice", "ff66c4");

        var result = await controller.DeleteEvent("BCDFGH", CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Single(Db.Events);
    }

    [Fact]
    public async Task DeleteEvent_WithValidOrganizer_RemovesEventAndRedirectsToHome()
    {
        var (evt, _, controller) = await CreateEventWithSignedInParticipantAsync();

        var result = await controller.DeleteEvent("BCDFGH", CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);

        Assert.Empty(Db.Events.Where(e => e.Id == evt.Id));
    }
}
