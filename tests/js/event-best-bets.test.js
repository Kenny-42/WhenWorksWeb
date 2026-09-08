// Covers event-best-bets.js's pure ranking/formatting/date-math logic only -- see
// FEATURES-vitest-js-test-coverage.ospec Step 2. renderBestBetsList/renderFinalDatesList's
// rendered markup is Step 3's job, not this file's.
//
// parseDateOnly/formatDateOnly/addDay aren't exposed on window.WWBestBets (only the six
// functions the IIFE assigns to it are), so they're exercised indirectly through
// computeFinalDateAvailability/getFinalDateKeys -- both walk a date range day-by-day using
// those helpers, so a skipped/duplicated/misaligned day surfaces in those functions' own
// results without needing to export anything just for testing.
import { beforeAll, describe, expect, it } from 'vitest';
import { loadScript } from './helpers/loadScript.js';

let WWBestBets;

beforeAll(() => {
    loadScript('event-best-bets.js');
    WWBestBets = window.WWBestBets;
});

describe('formatDateLabel', () => {
    it('formats a single-digit day with an abbreviated month name', () => {
        expect(WWBestBets.formatDateLabel('2026-01-05')).toBe('Jan 5');
    });

    it('formats a double-digit day', () => {
        expect(WWBestBets.formatDateLabel('2026-12-25')).toBe('Dec 25');
    });
});

describe('computeTopDates', () => {
    it('ranks entries by participant count descending', () => {
        const datesByKey = {
            '2026-01-01': [1],
            '2026-01-02': [1, 2, 3],
            '2026-01-03': [1, 2],
        };

        expect(WWBestBets.computeTopDates(datesByKey)).toEqual([
            { date: '2026-01-02', participantIds: [1, 2, 3] },
            { date: '2026-01-03', participantIds: [1, 2] },
            { date: '2026-01-01', participantIds: [1] },
        ]);
    });

    it('breaks ties by earliest date', () => {
        const datesByKey = {
            '2026-01-05': [1, 2],
            '2026-01-02': [1, 2],
            '2026-01-10': [1, 2],
        };

        const result = WWBestBets.computeTopDates(datesByKey);

        expect(result.map((entry) => entry.date)).toEqual([
            '2026-01-02',
            '2026-01-05',
            '2026-01-10',
        ]);
    });

    it('defaults to the top 3 entries', () => {
        const datesByKey = {
            '2026-01-01': [1, 2, 3, 4, 5],
            '2026-01-02': [1, 2, 3, 4],
            '2026-01-03': [1, 2, 3],
            '2026-01-04': [1, 2],
            '2026-01-05': [1],
        };

        const result = WWBestBets.computeTopDates(datesByKey);

        expect(result).toHaveLength(3);
        expect(result.map((entry) => entry.date)).toEqual([
            '2026-01-01',
            '2026-01-02',
            '2026-01-03',
        ]);
    });

    it('honors an overridden limit, above and below the default', () => {
        const datesByKey = {
            '2026-01-01': [1, 2, 3],
            '2026-01-02': [1, 2],
            '2026-01-03': [1],
        };

        expect(WWBestBets.computeTopDates(datesByKey, 1)).toHaveLength(1);
        expect(WWBestBets.computeTopDates(datesByKey, 10)).toHaveLength(3);
    });

    it('excludes entries with zero participants', () => {
        const datesByKey = {
            '2026-01-01': [],
            '2026-01-02': [1],
        };

        expect(WWBestBets.computeTopDates(datesByKey)).toEqual([
            { date: '2026-01-02', participantIds: [1] },
        ]);
    });
});

describe('computeFinalDateAvailability', () => {
    it('collapses a single-day entry to identical any-day/every-day sets', () => {
        const datesByKey = { '2026-01-01': [1, 2, 3] };
        const finalDate = { startDate: '2026-01-01', endDate: null };

        const result = WWBestBets.computeFinalDateAvailability(datesByKey, finalDate);

        expect(result.anyDayParticipantIds.sort()).toEqual([1, 2, 3]);
        expect(result.everyDayParticipantIds.sort()).toEqual([1, 2, 3]);
    });

    it('unions and intersects participants across a multi-day range', () => {
        const datesByKey = {
            '2026-01-01': [1, 2],
            '2026-01-02': [2, 3],
        };
        const finalDate = { startDate: '2026-01-01', endDate: '2026-01-02' };

        const result = WWBestBets.computeFinalDateAvailability(datesByKey, finalDate);

        expect(result.anyDayParticipantIds.sort()).toEqual([1, 2, 3]);
        expect(result.everyDayParticipantIds).toEqual([2]);
    });

    it('collapses the intersection to empty once a day has nobody available, and it stays empty', () => {
        const datesByKey = {
            '2026-01-01': [1, 2],
            '2026-01-02': [],
            '2026-01-03': [1, 2],
        };
        const finalDate = { startDate: '2026-01-01', endDate: '2026-01-03' };

        const result = WWBestBets.computeFinalDateAvailability(datesByKey, finalDate);

        expect(result.anyDayParticipantIds.sort()).toEqual([1, 2]);
        expect(result.everyDayParticipantIds).toEqual([]);
    });

    it('walks exactly one entry per day across a DST spring-forward transition', () => {
        // 2026-03-08 is the US spring-forward transition (2 AM local clocks jump to 3 AM).
        // A distinct participant per day means the union only equals all three ids -- and the
        // intersection only stays empty -- if all three days were visited exactly once each;
        // a day skipped or double-counted by the local-time Date math this walk relies on
        // internally would change one of those results.
        const datesByKey = {
            '2026-03-07': [1],
            '2026-03-08': [2],
            '2026-03-09': [3],
        };
        const finalDate = { startDate: '2026-03-07', endDate: '2026-03-09' };

        const result = WWBestBets.computeFinalDateAvailability(datesByKey, finalDate);

        expect(result.anyDayParticipantIds.sort()).toEqual([1, 2, 3]);
        expect(result.everyDayParticipantIds).toEqual([]);
    });
});

describe('getFinalDateKeys', () => {
    it('collects every key covered by a mix of single-day and range entries', () => {
        const finalDates = [
            { startDate: '2026-01-01', endDate: null },
            { startDate: '2026-02-10', endDate: '2026-02-12' },
        ];

        const keys = WWBestBets.getFinalDateKeys(finalDates);

        expect(Object.keys(keys).sort()).toEqual([
            '2026-01-01',
            '2026-02-10',
            '2026-02-11',
            '2026-02-12',
        ]);
    });

    it('walks exactly one key per day across a DST spring-forward transition', () => {
        const finalDates = [{ startDate: '2026-03-07', endDate: '2026-03-09' }];

        const keys = WWBestBets.getFinalDateKeys(finalDates);

        expect(Object.keys(keys).sort()).toEqual([
            '2026-03-07',
            '2026-03-08',
            '2026-03-09',
        ]);
    });
});
