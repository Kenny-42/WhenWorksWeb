using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using WhenWorksWeb.Hubs;

namespace WhenWorksWeb.Tests.Hubs;

/// <summary>
/// Unit tests for <see cref="EventHub"/> — group-membership management only, since the hub has no
/// other server-invoked methods (see the type's own remarks). <see cref="Hub.Context"/> and
/// <see cref="Hub.Groups"/> are public settable properties specifically so a hub can be exercised
/// without a real connection; both are substituted here (NSubstitute, per the "avoid Moq"
/// convention) rather than spinning up a real SignalR client.
/// </summary>
public class EventHubTests
{
    private static EventHub CreateHub(string connectionId, out IGroupManager groups)
    {
        groups = Substitute.For<IGroupManager>();

        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(connectionId);

        return new EventHub { Context = context, Groups = groups };
    }

    [Fact]
    public async Task JoinEvent_AddsCallerConnectionToNormalizedGroupName()
    {
        var hub = CreateHub("conn-1", out var groups);

        await hub.JoinEvent("bcdfgh");

        await groups.Received(1).AddToGroupAsync("conn-1", "event:BCDFGH", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinEvent_WithDifferentConnections_AddsEachToTheSameGroup()
    {
        var hubA = CreateHub("conn-a", out var groupsA);
        var hubB = CreateHub("conn-b", out var groupsB);

        await hubA.JoinEvent("BCDFGH");
        await hubB.JoinEvent("BCDFGH");

        await groupsA.Received(1).AddToGroupAsync("conn-a", "event:BCDFGH", Arg.Any<CancellationToken>());
        await groupsB.Received(1).AddToGroupAsync("conn-b", "event:BCDFGH", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("bcdfgh", "event:BCDFGH")]
    [InlineData("BCDFGH", "event:BCDFGH")]
    [InlineData(" bcdfgh ", "event:BCDFGH")]
    public void GroupName_NormalizesTrimAndCase(string code, string expected)
    {
        Assert.Equal(expected, EventHub.GroupName(code));
    }
}
