using Microsoft.AspNetCore.Mvc;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Controllers;

/// <summary>
/// Tier 2 tests for <see cref="WhenWorksWeb.Controllers.EventsController"/>'s SignIn actions
/// (<c>EventsController.SignIn.cs</c>).
/// </summary>
public class EventsControllerSignInTests : EventsControllerTestFixture
{
    /// <summary>
    /// GET SignIn for a code with no matching event should return the shared "not found" page, not throw.
    /// </summary>
    [Fact]
    public async Task Get_WithNonExistentCode_ReturnsEventNotFoundView()
    {
        var (controller, _) = CreateController();

        var result = await controller.SignIn("ZZZZZZ", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Shared/Error.cshtml", viewResult.ViewName);
        var model = Assert.IsType<ErrorViewModel>(viewResult.Model);
        Assert.Equal("Event Not Found", model.Title);
    }

    /// <summary>
    /// GET SignIn for an existing event, with no access cookie and no signed-in user, should render the sign-in
    /// page with empty defaults and no rejoin code prompt.
    /// </summary>
    [Fact]
    public async Task Get_WithExistingEventAndNoParticipant_ReturnsEmptySignInForm()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").WithTitle("Trivia Night").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();

        var result = await controller.SignIn("bcdfgh", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<EventSignInViewModel>(viewResult.Model);
        Assert.Equal("BCDFGH", model.Code);
        Assert.Equal("Trivia Night", model.EventName);
        Assert.Equal(string.Empty, model.DisplayName);
        Assert.False(model.ShowRejoinCodeInput);
        Assert.Empty(model.ExistingParticipants);
    }

    /// <summary>
    /// A valid new-participant submission should create the participant, save it, set the event access cookie,
    /// and redirect to the event home page.
    /// </summary>
    [Fact]
    public async Task Post_NewParticipant_WithValidData_CreatesParticipantSetsCookieAndRedirects()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var (controller, httpContext) = CreateController();
        var model = new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "ff66c4" };

        var result = await controller.SignIn("BCDFGH", model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventHome", redirect.RouteName);

        var savedParticipant = Assert.Single(Db.Participants);
        Assert.Equal("Alice", savedParticipant.DisplayName);
        Assert.Equal("ff66c4", savedParticipant.Color);
        Assert.NotNull(savedParticipant.RejoinCode);

        var cookieValue = ControllerTestContext.GetResponseCookieValue(httpContext, "WhenWorksWeb.EventAccess.BCDFGH");
        Assert.NotNull(cookieValue);
    }

    /// <summary>
    /// A display name that's already taken by another participant in the same event should be rejected with a
    /// model error and must not create a second participant.
    /// </summary>
    [Fact]
    public async Task Post_NewParticipant_WithDuplicateDisplayName_AddsModelErrorAndPersistsNothing()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();
        Db.Participants.Add(new ParticipantBuilder().ForEvent(evt).WithDisplayName("Alice").WithColor("111111").Build());
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();
        var model = new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "222222" };

        var result = await controller.SignIn("BCDFGH", model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(
            controller.ModelState[nameof(EventSignInViewModel.DisplayName)]!.Errors,
            e => e.ErrorMessage == "That display name is already taken in this event.");
        Assert.Single(Db.Participants); // only the pre-seeded one
    }

    /// <summary>
    /// A color that's already taken by another participant in the same event should be rejected with a model error.
    /// </summary>
    [Fact]
    public async Task Post_NewParticipant_WithDuplicateColor_AddsModelError()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();
        Db.Participants.Add(new ParticipantBuilder().ForEvent(evt).WithDisplayName("Alice").WithColor("111111").Build());
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();
        var model = new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Bob", Color = "111111" };

        var result = await controller.SignIn("BCDFGH", model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(EventSignInViewModel.Color)]!.Errors,
            e => e.ErrorMessage == "That color is already taken in this event.");
    }

    /// <summary>
    /// Selecting an existing, unowned participant without supplying a rejoin code should be rejected — a rejoin
    /// code is required whenever the signed-in session doesn't already own the participant.
    /// </summary>
    [Fact]
    public async Task Post_ExistingParticipant_WithMissingRejoinCode_AddsModelError()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();
        var existing = new ParticipantBuilder().ForEvent(evt).WithDisplayName("Alice").WithRejoinCode("BCDFGH").Build();
        Db.Participants.Add(existing);
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();
        var model = new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "ff66c4", SelectedExistingDisplayName = "Alice" };

        var result = await controller.SignIn("BCDFGH", model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(EventSignInViewModel.RejoinCode)]!.Errors,
            e => e.ErrorMessage == "Rejoin code is required.");
    }

    /// <summary>
    /// Selecting an existing, unowned participant with the wrong rejoin code should be rejected — matching is
    /// case-insensitive so this proves it's actually comparing values, not just requiring presence.
    /// </summary>
    [Fact]
    public async Task Post_ExistingParticipant_WithIncorrectRejoinCode_AddsModelError()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();
        Db.Participants.Add(new ParticipantBuilder().ForEvent(evt).WithDisplayName("Alice").WithRejoinCode("BCDFGH").Build());
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();
        var model = new EventSignInViewModel
        {
            Code = "BCDFGH",
            DisplayName = "Alice",
            Color = "ff66c4",
            SelectedExistingDisplayName = "Alice",
            RejoinCode = "MNPQRS"
        };

        var result = await controller.SignIn("BCDFGH", model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(EventSignInViewModel.RejoinCode)]!.Errors,
            e => e.ErrorMessage == "The rejoin code is incorrect.");
    }

    /// <summary>
    /// Selecting an existing, unowned participant with the correct rejoin code (different case, since rejoin
    /// codes are case-insensitive) should update the participant and redirect to the event home page.
    /// </summary>
    [Fact]
    public async Task Post_ExistingParticipant_WithCorrectRejoinCode_UpdatesParticipantAndRedirects()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();
        Db.Participants.Add(new ParticipantBuilder().ForEvent(evt).WithDisplayName("Alice").WithColor("111111").WithRejoinCode("PQRSTV").Build());
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();
        var model = new EventSignInViewModel
        {
            Code = "BCDFGH",
            DisplayName = "Alice",
            Color = "222222",
            SelectedExistingDisplayName = "Alice",
            RejoinCode = "pqrstv"
        };

        var result = await controller.SignIn("BCDFGH", model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventHome", redirect.RouteName);

        var updated = Assert.Single(Db.Participants);
        Assert.Equal("222222", updated.Color);
    }

    /// <summary>
    /// Selecting a participant already linked to a different signed-in account should be rejected outright,
    /// without ever prompting for a rejoin code.
    /// </summary>
    [Fact]
    public async Task Post_ExistingParticipant_OwnedByAnotherAccount_AddsConflictModelError()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var owner = new ApplicationUserBuilder().WithUserName("owner").WithEmail("owner@example.com").Build();
        Db.Users.Add(owner);
        await Db.SaveChangesAsync();

        Db.Participants.Add(new ParticipantBuilder().ForEvent(evt).WithDisplayName("Alice").WithUserId(owner.Id).Build());
        await Db.SaveChangesAsync();

        var currentUser = new ApplicationUserBuilder().WithUserName("someoneelse").WithEmail("someoneelse@example.com").Build();
        Db.Users.Add(currentUser);
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController(currentUser);
        var model = new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "ff66c4", SelectedExistingDisplayName = "Alice" };

        var result = await controller.SignIn("BCDFGH", model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(EventSignInViewModel.SelectedExistingDisplayName)]!.Errors,
            e => e.ErrorMessage == "That participant is already associated with another account.");
    }
}
