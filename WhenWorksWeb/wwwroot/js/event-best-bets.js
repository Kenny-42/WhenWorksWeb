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

    global.WWBestBets = {
        formatDateLabel: formatDateLabel,
        computeTopDates: computeTopDates,
        renderBestBetsList: renderBestBetsList
    };
})(window);
