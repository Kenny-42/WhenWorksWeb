// Covers event-best-bets.js's pure ranking/formatting/date-math logic (see
// FEATURES-vitest-js-test-coverage.ospec Step 2) and renderBestBetsList/renderFinalDatesList's
// rendered markup (Step 3) -- one test file per source file, per this initiative's mirrored-path
// convention.
//
// parseDateOnly/formatDateOnly/addDay aren't exposed on window.WWBestBets (only the six
// functions the IIFE assigns to it are), so they're exercised indirectly through
// computeFinalDateAvailability/getFinalDateKeys -- both walk a date range day-by-day using
// those helpers, so a skipped/duplicated/misaligned day surfaces in those functions' own
// results without needing to export anything just for testing.
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
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

describe('renderBestBetsList', () => {
    let container;

    beforeEach(() => {
        container = document.createElement('div');
    });

    it('renders one row per ranked entry, up to the limit', () => {
        const datesByKey = {
            '2026-01-01': [1],
            '2026-01-02': [1, 2],
            '2026-01-03': [1, 2, 3],
        };
        const participantsById = {
            1: { displayName: 'A', color: 'ff0000' },
            2: { displayName: 'B', color: '00ff00' },
            3: { displayName: 'C', color: '0000ff' },
        };

        WWBestBets.renderBestBetsList(container, datesByKey, participantsById, 3);

        expect(container.querySelectorAll('.ww-best-bet-row').length).toBe(3);
    });

    it('honors an overridden limit when rendering', () => {
        const datesByKey = {
            '2026-01-01': [1],
            '2026-01-02': [1, 2],
            '2026-01-03': [1, 2, 3],
        };
        const participantsById = {
            1: { displayName: 'A', color: 'ff0000' },
            2: { displayName: 'B', color: '00ff00' },
            3: { displayName: 'C', color: '0000ff' },
        };

        WWBestBets.renderBestBetsList(container, datesByKey, participantsById, 3, { limit: 1 });

        expect(container.querySelectorAll('.ww-best-bet-row').length).toBe(1);
    });

    it('renders the empty-state row when there are no ranked entries', () => {
        WWBestBets.renderBestBetsList(container, {}, {}, 0);

        const rows = container.querySelectorAll('.ww-best-bet-row');
        expect(rows).toHaveLength(1);
        expect(rows[0].classList.contains('ww-best-bet-empty')).toBe(true);

        const message = rows[0].querySelector('p.text-muted.mb-0');
        expect(message).not.toBeNull();
        expect(message.textContent).toBe('The best date will appear once your group starts picking.');
    });

    it('uses options.emptyText in place of the default empty-state copy', () => {
        WWBestBets.renderBestBetsList(container, {}, {}, 0, { emptyText: 'Nothing yet.' });

        expect(container.querySelector('p.text-muted.mb-0').textContent).toBe('Nothing yet.');
    });

    it('renders a plain, non-interactive div row when options.onSelect is not supplied', () => {
        const datesByKey = { '2026-01-01': [1] };
        const participantsById = { 1: { displayName: 'A', color: 'ff0000' } };

        WWBestBets.renderBestBetsList(container, datesByKey, participantsById, 1);

        const row = container.querySelector('.ww-best-bet-row');
        expect(row.tagName).toBe('DIV');
        expect(row.classList.contains('ww-best-bet-row-button')).toBe(false);
    });

    it('renders a clickable button row that invokes options.onSelect with the row\'s date when supplied', () => {
        const datesByKey = { '2026-01-01': [1] };
        const participantsById = { 1: { displayName: 'A', color: 'ff0000' } };
        const onSelect = vi.fn();

        WWBestBets.renderBestBetsList(container, datesByKey, participantsById, 1, { onSelect });

        const row = container.querySelector('.ww-best-bet-row');
        expect(row.tagName).toBe('BUTTON');
        expect(row.classList.contains('ww-best-bet-row-button')).toBe(true);
        expect(row.type).toBe('button');

        row.click();
        expect(onSelect).toHaveBeenCalledExactlyOnceWith('2026-01-01');
    });

    it('renders one trailing dot per participant available on the row\'s date', () => {
        const datesByKey = { '2026-01-01': [1, 2, 3] };
        const participantsById = {
            1: { displayName: 'A', color: 'ff0000' },
            2: { displayName: 'B', color: '00ff00' },
            3: { displayName: 'C', color: '0000ff' },
        };

        WWBestBets.renderBestBetsList(container, datesByKey, participantsById, 3);

        const dots = container.querySelectorAll('.ww-best-bet-dots .ww-best-bet-dot');
        expect(dots).toHaveLength(3);
        expect(Array.from(dots).map((dot) => dot.style.backgroundColor)).toEqual([
            'rgb(255, 0, 0)',
            'rgb(0, 255, 0)',
            'rgb(0, 0, 255)',
        ]);
    });

    it('falls back to a default gray dot for a participant id missing from participantsById', () => {
        const datesByKey = { '2026-01-01': [1] };

        WWBestBets.renderBestBetsList(container, datesByKey, {}, 1);

        const dot = container.querySelector('.ww-best-bet-dots .ww-best-bet-dot');
        expect(dot.style.backgroundColor).toBe('rgb(204, 204, 204)');
    });
});

describe('renderFinalDatesList', () => {
    let container;

    beforeEach(() => {
        container = document.createElement('div');
    });

    it('renders one row per final date entry', () => {
        const finalDates = [
            { startDate: '2026-01-01', endDate: null },
            { startDate: '2026-02-10', endDate: '2026-02-12' },
        ];

        WWBestBets.renderFinalDatesList(container, finalDates, {}, {}, 0);

        expect(container.querySelectorAll('.ww-best-bet-row')).toHaveLength(2);
    });

    it('renders a single-day entry with one count line and no en-dash range in the label', () => {
        const finalDates = [{ startDate: '2026-01-01', endDate: null }];
        const datesByKey = { '2026-01-01': [1, 2] };

        WWBestBets.renderFinalDatesList(container, finalDates, datesByKey, {}, 2);

        const row = container.querySelector('.ww-best-bet-row');
        expect(row.querySelector('.ww-best-bet-date').textContent).toBe('Jan 1');
        expect(row.querySelectorAll('.ww-best-bet-count')).toHaveLength(1);
        expect(row.querySelector('.ww-best-bet-count').textContent).toBe('2 of 2 available');
    });

    it('renders a range entry with both every-day and some-days count lines and an en-dash label', () => {
        const finalDates = [{ startDate: '2026-01-01', endDate: '2026-01-02' }];
        const datesByKey = {
            '2026-01-01': [1, 2],
            '2026-01-02': [2, 3],
        };

        WWBestBets.renderFinalDatesList(container, finalDates, datesByKey, {}, 3);

        const row = container.querySelector('.ww-best-bet-row');
        expect(row.querySelector('.ww-best-bet-date').textContent).toBe('Jan 1 – Jan 2');

        const counts = row.querySelectorAll('.ww-best-bet-count');
        expect(counts).toHaveLength(2);
        expect(counts[0].textContent).toBe('1 of 3 available every day');
        expect(counts[1].textContent).toBe('3 of 3 available some days');
    });

    it('appends dots directly to the row when options.renderRemoveControl is not supplied', () => {
        const finalDates = [{ startDate: '2026-01-01', endDate: null }];
        const datesByKey = { '2026-01-01': [1] };
        const participantsById = { 1: { displayName: 'A', color: 'ff0000' } };

        WWBestBets.renderFinalDatesList(container, finalDates, datesByKey, participantsById, 1);

        const row = container.querySelector('.ww-best-bet-row');
        expect(row.querySelector('.ww-final-date-row-trailing')).toBeNull();
        expect(row.querySelector(':scope > .ww-best-bet-dots')).not.toBeNull();
    });

    it('wraps dots and the supplied remove control together in a trailing group when options.renderRemoveControl is supplied', () => {
        const finalDates = [{ startDate: '2026-01-01', endDate: null }];
        const datesByKey = { '2026-01-01': [1] };
        const participantsById = { 1: { displayName: 'A', color: 'ff0000' } };
        const renderRemoveControl = vi.fn((finalDate) => {
            const form = document.createElement('form');
            form.className = 'ww-remove-control';
            form.dataset.date = finalDate.startDate;
            return form;
        });

        WWBestBets.renderFinalDatesList(container, finalDates, datesByKey, participantsById, 1, {
            renderRemoveControl,
        });

        const row = container.querySelector('.ww-best-bet-row');
        const trailing = row.querySelector(':scope > .ww-final-date-row-trailing');
        expect(trailing).not.toBeNull();
        expect(trailing.querySelector('.ww-best-bet-dots')).not.toBeNull();
        expect(trailing.querySelector('.ww-remove-control')).not.toBeNull();
        expect(renderRemoveControl).toHaveBeenCalledExactlyOnceWith(finalDates[0]);
    });
});
