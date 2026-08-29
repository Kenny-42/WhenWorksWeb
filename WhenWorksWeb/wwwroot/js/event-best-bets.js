// Shared "best bets" ranking + rendering, used by both the Availability tab's sidebar
// (Views/Events/Home.cshtml) and the Settings tab's "Call the date" suggestions list
// (Views/Events/Settings.cshtml) so the two stay in sync from one source of truth.
//
// Both pages already have a 'yyyy-MM-dd' -> participantIds[] map (datesByKey) and a
// participantId -> { displayName, color } map (participantsById) built from the same
// EventCalendarViewModel JSON; this module operates on those shapes directly rather than
// owning its own copy of the calendar data.
(function (global) {
    "use strict";

    var MONTH_NAMES = [
        'January', 'February', 'March', 'April', 'May', 'June',
        'July', 'August', 'September', 'October', 'November', 'December'
    ];

    function formatDateLabel(key) {
        var parts = key.split('-');
        var d = new Date(parseInt(parts[0], 10), parseInt(parts[1], 10) - 1, parseInt(parts[2], 10));
        return MONTH_NAMES[d.getMonth()].slice(0, 3) + ' ' + d.getDate();
    }

    function colorFor(participantsById, participantId) {
        var participant = participantsById[participantId];
        return participant ? participant.color : 'cccccc';
    }

    function pad(n) {
        return n < 10 ? '0' + n : '' + n;
    }

    // Parses a 'yyyy-MM-dd' string as a UTC midnight Date — UTC throughout (not local time) so
    // day-by-day iteration below can't be thrown off by a DST transition landing inside the range.
    function parseDateOnly(key) {
        var parts = key.split('-');
        return new Date(Date.UTC(parseInt(parts[0], 10), parseInt(parts[1], 10) - 1, parseInt(parts[2], 10)));
    }

    function formatDateOnly(date) {
        return date.getUTCFullYear() + '-' + pad(date.getUTCMonth() + 1) + '-' + pad(date.getUTCDate());
    }

    function addDay(date) {
        return new Date(date.getTime() + 24 * 60 * 60 * 1000);
    }

    // Ranked by participant count descending, ties broken by earliest date, capped at `limit`
    // (default 3). Dates with zero participants (already-cleaned-up entries some callers may
    // still be holding a stale key for) are excluded.
    function computeTopDates(datesByKey, limit) {
        var entries = Object.keys(datesByKey)
            .map(function (key) { return { date: key, participantIds: datesByKey[key] }; })
            .filter(function (entry) { return entry.participantIds.length > 0; });

        entries.sort(function (a, b) {
            if (b.participantIds.length !== a.participantIds.length) {
                return b.participantIds.length - a.participantIds.length;
            }
            return a.date < b.date ? -1 : (a.date > b.date ? 1 : 0);
        });

        return entries.slice(0, limit || 3);
    }

    // Renders the ranked list into `container` as '.ww-best-bet-row' entries (date + count +
    // per-participant color dots). `options.emptyText` replaces the default empty-state copy;
    // `options.limit` overrides the top-N cutoff; `options.onSelect(date)`, if given, makes each
    // row clickable (used by Settings to prefill the add-date form) instead of a static row.
    function renderBestBetsList(container, datesByKey, participantsById, totalParticipants, options) {
        options = options || {};

        var topEntries = computeTopDates(datesByKey, options.limit);
        container.innerHTML = '';

        if (topEntries.length === 0) {
            // Same card treatment as a real row (.ww-best-bet-row), not a bare paragraph, so the
            // empty state doesn't look unstyled next to the rows that will eventually replace it.
            var empty = document.createElement('div');
            empty.className = 'ww-best-bet-row ww-best-bet-empty';
            var emptyText = document.createElement('p');
            emptyText.className = 'text-muted mb-0';
            emptyText.textContent = options.emptyText || 'The best date will appear once your group starts picking.';
            empty.appendChild(emptyText);
            container.appendChild(empty);
            return;
        }

        topEntries.forEach(function (entry) {
            var row = document.createElement(options.onSelect ? 'button' : 'div');
            // '.ww-best-bet-row' alone supplies the fill/border/layout either way; the button
            // variant gets its own modifier for the cursor and for undoing a plain <button>'s
            // browser-default centered text (a div has no such default, so the div variant
            // doesn't need it).
            row.className = 'ww-best-bet-row' + (options.onSelect ? ' ww-best-bet-row-button' : '');
            if (options.onSelect) {
                row.type = 'button';
                row.addEventListener('click', function () {
                    options.onSelect(entry.date);
                });
            }

            var info = document.createElement('div');
            info.className = 'ww-best-bet-info';

            var dateLabel = document.createElement('span');
            dateLabel.className = 'ww-best-bet-date';
            dateLabel.textContent = formatDateLabel(entry.date);
            info.appendChild(dateLabel);

            var count = document.createElement('span');
            count.className = 'ww-best-bet-count';
            count.textContent = entry.participantIds.length + ' of ' + totalParticipants + ' available';
            info.appendChild(count);

            row.appendChild(info);

            var dots = document.createElement('span');
            dots.className = 'ww-best-bet-dots';
            entry.participantIds.forEach(function (id) {
                var dot = document.createElement('span');
                dot.className = 'ww-best-bet-dot';
                dot.style.backgroundColor = '#' + colorFor(participantsById, id);
                dots.appendChild(dot);
            });
            row.appendChild(dots);

            container.appendChild(row);
        });
    }

    // Walks every day in a final date entry's [StartDate, EndDate ?? StartDate] range against the
    // page's live datesByKey map, returning the union ("any day") and intersection ("every day")
    // of participant ids available across that range. A single-day entry's two sets are
    // identical by construction (one day to walk), which callers rely on to collapse a single-day
    // row down to one line instead of two. The intersection collapses to empty the moment one day
    // in the range has nobody picked — once empty it stays empty, since intersecting with the
    // empty set is a no-op for every day after.
    function computeFinalDateAvailability(datesByKey, finalDate) {
        var day = parseDateOnly(finalDate.startDate);
        var lastDay = finalDate.endDate ? parseDateOnly(finalDate.endDate) : day;

        var anyDaySet = {};
        var everyDaySet = null;

        for (; day <= lastDay; day = addDay(day)) {
            var ids = datesByKey[formatDateOnly(day)] || [];

            ids.forEach(function (id) {
                anyDaySet[id] = true;
            });

            if (everyDaySet === null) {
                everyDaySet = {};
                ids.forEach(function (id) {
                    everyDaySet[id] = true;
                });
            } else {
                var idsForDay = {};
                ids.forEach(function (id) {
                    idsForDay[id] = true;
                });
                Object.keys(everyDaySet).forEach(function (id) {
                    if (!idsForDay[id]) {
                        delete everyDaySet[id];
                    }
                });
            }
        }

        return {
            anyDayParticipantIds: Object.keys(anyDaySet).map(Number),
            everyDayParticipantIds: Object.keys(everyDaySet || {}).map(Number)
        };
    }

    // Renders a "Final dates" list: one row per final date, reusing the same
    // '.ww-best-bet-row'/-info/-date/-count/-dots/-dot styling as Best Bets. A single-day entry
    // shows one "N of M available" line; a range shows both the intersection ("every day") and
    // union ("some days") counts, with dots for the union set. Used non-interactively (no
    // `options`) for the Availability tab's live "Final dates" card, and again on the Finalize
    // tab's own "Final dates" card with `options.renderRemoveControl` supplied so the same-
    // looking rows there also get an organizer-only remove control — see that page's own script
    // for what it builds (a real <form> posting to the remove route).
    function renderFinalDatesList(container, finalDates, datesByKey, participantsById, totalParticipants, options) {
        options = options || {};
        container.innerHTML = '';

        finalDates.forEach(function (finalDate) {
            var availability = computeFinalDateAvailability(datesByKey, finalDate);
            var isRange = Boolean(finalDate.endDate);

            var row = document.createElement('div');
            row.className = 'ww-best-bet-row';

            var info = document.createElement('div');
            info.className = 'ww-best-bet-info';

            var dateLabel = document.createElement('span');
            dateLabel.className = 'ww-best-bet-date';
            dateLabel.textContent = isRange
                ? formatDateLabel(finalDate.startDate) + ' – ' + formatDateLabel(finalDate.endDate)
                : formatDateLabel(finalDate.startDate);
            info.appendChild(dateLabel);

            var everyDayCount = document.createElement('span');
            everyDayCount.className = 'ww-best-bet-count';
            everyDayCount.textContent = availability.everyDayParticipantIds.length + ' of ' + totalParticipants +
                ' available' + (isRange ? ' every day' : '');
            info.appendChild(everyDayCount);

            if (isRange) {
                var someDaysCount = document.createElement('span');
                someDaysCount.className = 'ww-best-bet-count';
                someDaysCount.textContent = availability.anyDayParticipantIds.length + ' of ' + totalParticipants + ' available some days';
                info.appendChild(someDaysCount);
            }

            row.appendChild(info);

            var dots = document.createElement('span');
            dots.className = 'ww-best-bet-dots';
            availability.anyDayParticipantIds.forEach(function (id) {
                var dot = document.createElement('span');
                dot.className = 'ww-best-bet-dot';
                dot.style.backgroundColor = '#' + colorFor(participantsById, id);
                dots.appendChild(dot);
            });

            if (options.renderRemoveControl) {
                // Dots + remove control share one trailing flex group instead of each being its
                // own direct child of the row — the row's own space-between only reads right with
                // exactly two children (info on the left, everything else on the right); a third
                // direct child would instead get evenly spaced from both neighbors, stranding the
                // dots in the middle of the row.
                var trailing = document.createElement('span');
                trailing.className = 'ww-final-date-row-trailing';
                trailing.appendChild(dots);
                trailing.appendChild(options.renderRemoveControl(finalDate));
                row.appendChild(trailing);
            } else {
                row.appendChild(dots);
            }

            container.appendChild(row);
        });
    }

    global.WWBestBets = {
        formatDateLabel: formatDateLabel,
        computeTopDates: computeTopDates,
        renderBestBetsList: renderBestBetsList,
        computeFinalDateAvailability: computeFinalDateAvailability,
        renderFinalDatesList: renderFinalDatesList
    };
})(window);
