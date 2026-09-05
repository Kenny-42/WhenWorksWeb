using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Small reflection helpers for asserting against a broadcast sent through
/// <see cref="EventsControllerTestFixture.HubClientProxy"/> — <see cref="IClientProxy.SendAsync(string, object, CancellationToken)"/>
/// is an extension method over the real interface member, <c>SendCoreAsync</c>, so a substituted
/// <see cref="IClientProxy"/>'s recorded calls have to be read from there instead. The broadcast
/// payload itself is always an anonymous type (<c>new { date, participantIds }</c>, etc.), so its
/// properties are read by name via reflection — the same approach the controller tests already use
/// for a <see cref="Microsoft.AspNetCore.Mvc.JsonResult"/>'s anonymous <c>Value</c>.
/// </summary>
internal static class HubBroadcastTestHelper
{
    /// <summary>
    /// Returns the hub method name and payload object of the most recent <c>SendCoreAsync</c> call
    /// received by <paramref name="proxy"/>, or null if it was never called.
    /// </summary>
    public static (string Method, object Payload)? GetLastBroadcast(IClientProxy proxy)
    {
        var call = proxy.ReceivedCalls()
            .LastOrDefault(c => c.GetMethodInfo().Name == nameof(IClientProxy.SendCoreAsync));

        if (call is null)
        {
            return null;
        }

        var arguments = call.GetArguments();
        var method = (string)arguments[0]!;
        var payloadArgs = (object?[])arguments[1]!;
        return (method, payloadArgs[0]!);
    }

    /// <summary>Reads a named property off an anonymous broadcast payload object.</summary>
    public static T GetPayloadProperty<T>(object payload, string propertyName)
    {
        var value = payload.GetType().GetProperty(propertyName)!.GetValue(payload);
        return (T)value!;
    }
}
