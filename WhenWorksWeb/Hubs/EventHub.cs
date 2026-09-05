using Microsoft.AspNetCore.SignalR;

namespace WhenWorksWeb.Hubs;

/// <summary>
/// Live-sync hub for a single event: one group per event (keyed by its code), joined by every
/// connected viewer of that event's Home or Finalize page. <see cref="Controllers.EventsController"/>
/// broadcasts availability/final-date changes to the group after they're saved (see
/// <c>EventsController.Availability.cs</c>/<c>EventsController.FinalDate.cs</c>) — this hub itself
/// only manages group membership, it has no server-invoked "action" methods of its own.
/// </summary>
/// <remarks>
/// No custom authorization: same trust model as the rest of the app today (see the feature spec,
/// <c>Spec/Features/FEATURES-live-sync-availability-calendar.ospec</c>) — anyone who can load the
/// event's page (i.e. anyone with its code) can join its group, exactly as anyone with the code
/// can already view/join the event itself. Participants are not ASP.NET Identity-authenticated
/// users, so there's no principal here to authorize against.
/// </remarks>
public sealed class EventHub : Hub
{
    /// <summary>
    /// Adds the caller's connection to the group for the event identified by <paramref name="code"/>.
    /// Called once by the client right after the hub connection starts (and again after every
    /// automatic reconnect, since group membership doesn't survive a dropped connection).
    /// </summary>
    public Task JoinEvent(string code)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(code));
    }

    /// <summary>
    /// Computes the SignalR group name for an event code — normalized the same way
    /// <c>EventsController.GetEventAsync</c> normalizes a code for lookup (trimmed, uppercased),
    /// so a group joined from the client's raw route-supplied code always matches the group the
    /// controller broadcasts to.
    /// </summary>
    internal static string GroupName(string code) => "event:" + code.Trim().ToUpperInvariant();
}
