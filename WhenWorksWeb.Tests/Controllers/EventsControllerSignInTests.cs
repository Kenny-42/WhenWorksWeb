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
    /// page with empty defaults.
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
    /// Selecting an existing, unowned guest participant should update it and redirect straight to the event
    /// home page — no rejoin code or other ownership proof is required (Issue #73: rejoin codes were removed
    /// so any guest can pick an existing guest display name from the dropdown).
    /// </summary>
    [Fact]
    public async Task Post_ExistingParticipant_SelectedWithoutOwnershipProof_UpdatesParticipantAndRedirects()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();
        Db.Participants.Add(new ParticipantBuilder().ForEvent(evt).WithDisplayName("Alice").WithColor("111111").Build());
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();
        var model = new EventSignInViewModel
        {
            Code = "BCDFGH",
            DisplayName = "Alice",
            Color = "222222",
            SelectedExistingDisplayName = "Alice"
        };

        var result = await controller.SignIn("BCDFGH", model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToRouteResult>(result);
        Assert.Equal("EventHome", redirect.RouteName);

        var updated = Assert.Single(Db.Participants);
        Assert.Equal("222222", updated.Color);
    }

    /// <summary>
    /// Selecting a participant already linked to a different signed-in account should still be rejected
    /// outright — removing the rejoin code doesn't weaken the account-ownership guard.
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

    /// <summary>
    /// A signed-out guest selecting a participant that's linked to a real account must be rejected exactly like
    /// a mismatched signed-in account would be — otherwise, with rejoin codes removed (Issue #73), an anonymous
    /// guest could take over an account-owned participant (rename it, recolor it, and receive its access
    /// cookie) just by picking its display name from the dropdown. Guests may only claim unowned participants.
    /// </summary>
    [Fact]
    public async Task Post_ExistingParticipant_OwnedByAccount_WithNoSignedInUser_AddsConflictModelError()
    {
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var owner = new ApplicationUserBuilder().WithUserName("owner").WithEmail("owner@example.com").Build();
        Db.Users.Add(owner);
        await Db.SaveChangesAsync();

        Db.Participants.Add(new ParticipantBuilder().ForEvent(evt).WithDisplayName("Alice").WithColor("111111").WithUserId(owner.Id).Build());
        await Db.SaveChangesAsync();

        var (controller, _) = CreateController();
        var model = new EventSignInViewModel { Code = "BCDFGH", DisplayName = "Alice", Color = "ff66c4", SelectedExistingDisplayName = "Alice" };

        var result = await controller.SignIn("BCDFGH", model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(EventSignInViewModel.SelectedExistingDisplayName)]!.Errors,
            e => e.ErrorMessage == "That participant is already associated with another account.");

        // The account-owned participant must be untouched — the rejected request must not have gone through.
        var alice = Db.Participants.Single(p => p.DisplayName == "Alice");
        Assert.Equal("111111", alice.Color);
        Assert.Equal(owner.Id, alice.UserId);
    }
}
