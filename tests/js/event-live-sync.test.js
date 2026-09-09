// Covers event-live-sync.js's fetchSnapshot and connect (see
// FEATURES-vitest-js-test-coverage.ospec Step 4) -- one test file per source file, per this
// initiative's mirrored-path convention.
//
// fetch and signalR are both mocked: fetchSnapshot is tested against a mocked global fetch,
// and connect against a stub HubConnectionBuilder/HubConnection standing in for the real
// signalR global -- no real network or SignalR connection is involved.
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { loadScript } from './helpers/loadScript.js';

let WWLiveSync;

beforeAll(() => {
    loadScript('event-live-sync.js');
    WWLiveSync = window.WWLiveSync;
});

describe('fetchSnapshot', () => {
    beforeEach(() => {
        window.fetch = vi.fn();
    });

    it('parses and returns the JSON body on a successful response', async () => {
        const payload = { dates: [{ date: '2026-01-01', participantIds: [1] }], finalDates: [] };
        window.fetch.mockResolvedValue({
            ok: true,
            json: () => Promise.resolve(payload),
        });

        const result = await WWLiveSync.fetchSnapshot('ABC123');

        expect(window.fetch).toHaveBeenCalledWith('/event/ABC123/calendar-snapshot');
        expect(result).toEqual(payload);
    });

    it('rejects when the response is not ok', async () => {
        window.fetch.mockResolvedValue({ ok: false, json: () => Promise.resolve({}) });

        await expect(WWLiveSync.fetchSnapshot('ABC123')).rejects.toThrow('Calendar snapshot fetch failed');
    });
});

describe('connect', () => {
    let connectionStub;

    beforeEach(() => {
        connectionStub = {
            on: vi.fn(),
            onreconnected: vi.fn(),
            invoke: vi.fn(() => Promise.resolve()),
            start: vi.fn(() => Promise.resolve()),
        };
        const builderStub = {
            withUrl: vi.fn(function () { return this; }),
            withAutomaticReconnect: vi.fn(function () { return this; }),
            build: vi.fn(() => connectionStub),
        };
        window.signalR = {
            HubConnectionBuilder: vi.fn(function () { return builderStub; }),
        };
    });

    it("joins the event's group once the connection starts", async () => {
        WWLiveSync.connect('ABC123', {});

        await vi.waitFor(() => {
            expect(connectionStub.invoke).toHaveBeenCalledWith('JoinEvent', 'ABC123');
        });
    });

    it('rejoins the group on reconnect', async () => {
        WWLiveSync.connect('ABC123', {});

        await vi.waitFor(() => expect(connectionStub.invoke).toHaveBeenCalledTimes(1));
        connectionStub.invoke.mockClear();

        const onReconnectedCallback = connectionStub.onreconnected.mock.calls[0][0];
        onReconnectedCallback();

        expect(connectionStub.invoke).toHaveBeenCalledWith('JoinEvent', 'ABC123');
    });

    it('calls the optional onReconnected handler on reconnect', async () => {
        const onReconnected = vi.fn();
        WWLiveSync.connect('ABC123', { onReconnected });

        await vi.waitFor(() => expect(connectionStub.invoke).toHaveBeenCalledTimes(1));

        const onReconnectedCallback = connectionStub.onreconnected.mock.calls[0][0];
        onReconnectedCallback();

        expect(onReconnected).toHaveBeenCalledTimes(1);
    });

    it('does not require an onReconnected handler to be provided', () => {
        expect(() => WWLiveSync.connect('ABC123', {})).not.toThrow();
    });

    it('wires the optional onAvailabilityChanged/onFinalDatesChanged handlers to their broadcasts', () => {
        const onAvailabilityChanged = vi.fn();
        const onFinalDatesChanged = vi.fn();

        WWLiveSync.connect('ABC123', { onAvailabilityChanged, onFinalDatesChanged });

        expect(connectionStub.on).toHaveBeenCalledWith('AvailabilityChanged', onAvailabilityChanged);
        expect(connectionStub.on).toHaveBeenCalledWith('FinalDatesChanged', onFinalDatesChanged);
    });

    it('does not subscribe to a broadcast whose handler was not provided', () => {
        WWLiveSync.connect('ABC123', {});

        expect(connectionStub.on).not.toHaveBeenCalled();
    });

    it('returns the live connection', () => {
        const result = WWLiveSync.connect('ABC123', {});

        expect(result).toBe(connectionStub);
    });
});
