// Shared live-sync connection helper, used by both the Availability tab (Views/Events/Home.cshtml)
// and the Finalize tab (Views/Events/Finalize.cshtml) so the SignalR plumbing — connecting,
// joining the event's group, reconnect handling — lives in one place, the same shared-module
// precedent as event-best-bets.js. Each page still owns its own rendering: this module only hands
// broadcast payloads and a reconnect signal back to whichever handlers the page registered.
(function (global) {
    "use strict";

    // Reconnect delays SignalR's built-in withAutomaticReconnect retries at, in milliseconds —
    // a few quick attempts, then settling into a slower steady cadence rather than hammering the
    // server indefinitely.
    var RECONNECT_DELAYS_MS = [0, 2000, 5000, 10000, 20000, 30000];

    /// <summary>
    /// Opens a hub connection, joins <c>code</c>'s group, and wires up whichever of
    /// <c>handlers.onAvailabilityChanged</c>/<c>handlers.onFinalDatesChanged</c>/
    /// <c>handlers.onReconnected</c> the caller provided (each optional — a page that doesn't care
    /// about a given broadcast simply doesn't pass that handler). Returns the live
    /// <c>signalR.HubConnection</c>, mainly so callers can read <c>connection.connectionId</c> to
    /// send along with their own POSTs (see ToggleAvailability's self-echo exclusion).
    /// </summary>
    function connect(code, handlers) {
        handlers = handlers || {};

        var connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/event')
            .withAutomaticReconnect(RECONNECT_DELAYS_MS)
            .build();

        if (handlers.onAvailabilityChanged) {
            connection.on('AvailabilityChanged', handlers.onAvailabilityChanged);
        }
        if (handlers.onFinalDatesChanged) {
            connection.on('FinalDatesChanged', handlers.onFinalDatesChanged);
        }

        function joinEvent() {
            // Swallowed rather than surfaced — matches toggleDate()'s own "no dedicated error UI
            // for this pass" precedent. A failed join just means this tab misses live updates
            // until the next successful (re)connect; the page itself still works from its own
            // fetch responses either way.
            connection.invoke('JoinEvent', code).catch(function () { });
        }

        // Group membership doesn't survive a dropped connection, so it's rejoined on every
        // reconnect, not just the initial start. onReconnected (if given) is the page's cue to
        // fetch a fresh snapshot and reconcile in case a broadcast was missed while disconnected.
        connection.onreconnected(function () {
            joinEvent();
            if (handlers.onReconnected) {
                handlers.onReconnected();
            }
        });

        connection.start()
            .then(joinEvent)
            .catch(function () { });

        return connection;
    }

    /// <summary>
    /// Fetches the reconnect-catch-up snapshot (current candidate dates + final dates) for
    /// <c>code</c>. Returns a promise of the parsed <c>{ dates, finalDates }</c> JSON — the same
    /// shapes as <c>EventCalendarViewModel.Dates</c>/<c>FinalDates</c>.
    /// </summary>
    function fetchSnapshot(code) {
        return fetch('/event/' + code + '/calendar-snapshot')
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Calendar snapshot fetch failed');
                }
                return response.json();
            });
    }

    global.WWLiveSync = {
        connect: connect,
        fetchSnapshot: fetchSnapshot
    };
})(window);
