using Microsoft.AspNetCore.Mvc;
using WhenWorksWeb.Controllers;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.Fixtures;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Controllers;

/// <summary>
/// Tier 2 tests for <see cref="MyEventsController"/>.
/// </summary>
public class MyEventsControllerTests : SqliteDbContextFixture
{
    private MyEventsController CreateController(ApplicationUser? user, out Microsoft.AspNetCore.Http.DefaultHttpContext httpContext)
    {
        var controller = new MyEventsController(Db, TestUserManagerFactory.Create(Db));
        httpContext = ControllerTestContext.AttachContext(controller, user);
        return controller;
    }

    private async Task<ApplicationUser> AddUserAsync(string userName)
    {
        var user = new ApplicationUserBuilder().WithUserName(userName).WithEmail($"{userName}@example.com").Build();
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// An unauthenticated request to Index should be challenged (redirected to login), not throw or show an empty page.
    /// </summary>
    [Fact]
    public async Task Index_WhenNotAuthenticated_ReturnsChallenge()
    {
        var controller = CreateController(user: null, out _);

        var result = await controller.Index(CancellationToken.None);

        Assert.IsType<ChallengeResult>(result);
    }

    /// <summary>
    /// Index should list events the user created and events they've joined as a participant, and exclude
    /// events they have no relationship to.
    /// </summary>
    [Fact]
    public async Task Index_WhenAuthenticated_ListsCreatedAndJoinedEventsOnly()
    {
        var currentUser = await AddUserAsync("alice");

        var createdEvent = new EventBuilder().WithCode("BCDFGH").WithTitle("Created By Me").WithCreatedByUserId(currentUser.Id).Build();
        var joinedEvent = new EventBuilder().WithCode("MNPQRS").WithTitle("Joined Event").Build();
        var unrelatedEvent = new EventBuilder().WithCode("TVWXYZ").WithTitle("Unrelated Event").Build();
        Db.Events.AddRange(createdEvent, joinedEvent, unrelatedEvent);
        await Db.SaveChangesAsync();

        Db.Participants.Add(new ParticipantBuilder().ForEvent(joinedEvent).WithUserId(currentUser.Id).WithDisplayName("Alice").Build());
        await Db.SaveChangesAsync();

        var controller = CreateController(currentUser, out _);

        var result = await controller.Index(CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<MyEventViewModel>>(viewResult.Model);
        var titles = model.Select(m => m.Title).ToList();

        Assert.Contains("Created By Me", titles);
        Assert.Contains("Joined Event", titles);
        Assert.DoesNotContain("Unrelated Event", titles);
    }

    /// <summary>
    /// An unauthenticated Delete request should be challenged, the same as Index.
    /// </summary>
    [Fact]
    public async Task Delete_WhenNotAuthenticated_ReturnsChallenge()
    {
        var controller = CreateController(user: null, out _);

        var result = await controller.Delete(eventId: 1, participantId: null, deleteMode: "event", CancellationToken.None);

        Assert.IsType<ChallengeResult>(result);
    }

    /// <summary>
    /// The event's creator deleting it should remove the event and redirect back to the list.
    /// </summary>
    [Fact]
    public async Task Delete_Event_ByCreator_RemovesEventAndRedirects()
    {
        var creator = await AddUserAsync("creator");
        var evt = new EventBuilder().WithCode("BCDFGH").WithCreatedByUserId(creator.Id).Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var controller = CreateController(creator, out _);

        var result = await controller.Delete(evt.Id, participantId: null, deleteMode: "event", CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(Db.Events);
    }

    /// <summary>
    /// A user who did not create the event must not be able to delete it — Forbid, not silently succeed or 404.
    /// </summary>
    [Fact]
    public async Task Delete_Event_ByNonCreator_ReturnsForbidAndPersistsEvent()
    {
        var creator = await AddUserAsync("creator");
        var otherUser = await AddUserAsync("otheruser");
        var evt = new EventBuilder().WithCode("BCDFGH").WithCreatedByUserId(creator.Id).Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var controller = CreateController(otherUser, out _);

        var result = await controller.Delete(evt.Id, participantId: null, deleteMode: "event", CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Single(Db.Events);
    }

    /// <summary>
    /// Deleting a non-existent event id should return NotFound rather than throwing.
    /// </summary>
    [Fact]
    public async Task Delete_Event_NonExistent_ReturnsNotFound()
    {
        var currentUser = await AddUserAsync("alice");
        var controller = CreateController(currentUser, out _);

        var result = await controller.Delete(eventId: 999_999, participantId: null, deleteMode: "event", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// A participant leaving an event should be removed, and their prior chat messages should have their
    /// ParticipantId nulled out (not deleted) so message history survives — see CODING_CONVENTIONS.md's
    /// EventMessage.ParticipantId gotcha. This is exactly the FK-orphaning behavior a real database is needed
    /// to catch: EventMessage.ParticipantId has DeleteBehavior.NoAction, so if the controller ever stopped
    /// nulling it out first, this test would fail with a foreign key violation instead of silently passing.
    /// </summary>
    [Fact]
    public async Task Delete_Participant_ByOwner_RemovesParticipantAndOrphansMessages()
    {
        var currentUser = await AddUserAsync("alice");
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var participant = new ParticipantBuilder().ForEvent(evt).WithUserId(currentUser.Id).WithDisplayName("Alice").Build();
        Db.Participants.Add(participant);
        await Db.SaveChangesAsync();

        var message = new EventMessageBuilder().ForEvent(evt).FromParticipant(participant.Id).Build();
        Db.EventMessages.Add(message);
        await Db.SaveChangesAsync();

        var controller = CreateController(currentUser, out _);

        var result = await controller.Delete(evt.Id, participant.Id, deleteMode: "participant", CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(Db.Participants);

        var survivingMessage = Assert.Single(Db.EventMessages);
        Assert.Null(survivingMessage.ParticipantId);
    }

    /// <summary>
    /// Leaving an event without specifying which participant record to remove should be rejected as a bad
    /// request rather than guessing.
    /// </summary>
    [Fact]
    public async Task Delete_Participant_WithoutParticipantId_ReturnsBadRequest()
    {
        var currentUser = await AddUserAsync("alice");
        var controller = CreateController(currentUser, out _);

        var result = await controller.Delete(eventId: 1, participantId: null, deleteMode: "participant", CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
    }

    /// <summary>
    /// A participant id that doesn't belong to the current user (or doesn't exist) should return NotFound,
    /// not allow deleting someone else's participant record.
    /// </summary>
    [Fact]
    public async Task Delete_Participant_NotOwnedByCurrentUser_ReturnsNotFound()
    {
        var owner = await AddUserAsync("owner");
        var otherUser = await AddUserAsync("otheruser");
        var evt = new EventBuilder().WithCode("BCDFGH").Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var participant = new ParticipantBuilder().ForEvent(evt).WithUserId(owner.Id).WithDisplayName("Owner").Build();
        Db.Participants.Add(participant);
        await Db.SaveChangesAsync();

        var controller = CreateController(otherUser, out _);

        var result = await controller.Delete(evt.Id, participant.Id, deleteMode: "participant", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Single(Db.Participants);
    }

    /// <summary>
    /// An unrecognized deleteMode value should be rejected as a bad request.
    /// </summary>
    [Fact]
    public async Task Delete_WithUnrecognizedDeleteMode_ReturnsBadRequest()
    {
        var currentUser = await AddUserAsync("alice");
        var controller = CreateController(currentUser, out _);

        var result = await controller.Delete(eventId: 1, participantId: null, deleteMode: "not-a-real-mode", CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
    }
}
