---
name: create-issue
description: Interview the user about a bug, feature, or epic they want tracked, draft a GitHub issue in their precise/thorough style using the repo's issue templates, create it on GitHub if write access is available, and create the matching Spec/*.ospec entry referencing the issue number. Use when the user says things like "create an issue for...", "I want to file a bug about...", "let's write up this feature", or "/create-issue".
---

# Create Issue

Turns a conversation into (1) a GitHub issue matching this repo's established style and
(2) a `Spec/` entry that references it, per the convention in `CLAUDE.md`'s "Specs (Bug &
Feature Planning)" and "GitHub Integration" sections — read those two sections first if
you haven't already this session.

## 1. Determine the category

Ask (or infer, then confirm) which of the three this is:
- **Bug** — something is broken or behaving incorrectly. Template: `bug_report.md`. Spec folder: `Spec/Bugs/`.
- **Feature** — a single, self-contained piece of new/changed functionality. Template: `feature_request.md`. Spec folder: `Spec/Features/`.
- **Epic** — a larger body of work that will be broken into linked sub-issues. Template: `epic.md`. See step 3a — sub-issues are generated as part of this same conversation, not deferred to a later one.

## 2. Interview thoroughly

The user's existing issues (e.g. #52, #50, #38, #36, #40) are precise and detail-oriented —
short on filler, specific on steps/files/behavior, and they scale detail to the size of the
item (a one-line sub-issue gets one line; a multi-layer feature gets numbered steps and a
notes list). Match that. Don't accept a vague one-line description as final for anything
non-trivial; ask follow-ups until you have what the template needs.

For a **bug**, covering every section of `bug_report.md`:
- Exact steps to reproduce, in order.
- The page/route/flow it happens in.
- What should happen (expected) vs. what actually happens (actual — error text, wrong
  value, silent failure, etc.).
- Whether they have a screenshot, and browser/version if it's a rendering/UI bug.
- Frequency (always / intermittent / one-time) if it's not obviously always.
- If the user has a hunch about the cause or the relevant file, capture it — but don't
  invent a root cause they haven't confirmed. Root-cause investigation (with file:line
  references) happens later (step 5), by actually reading the code, not by guessing during
  the interview.

For a **feature**:
- What should be built, and why (the problem it solves, not just the mechanism).
- **Requirements is mandatory** — always get at least the specific, concrete behaviors the
  implementation must satisfy (like #36), even for a small item; don't leave it as the
  template placeholder.
- **Steps stays optional** and sits alongside Requirements, not instead of it — add it
  when the work is naturally sequential/build-order-dependent (like #38's "update the
  controller, then the view, then add the endpoint" ordering).
- Constraints, exclusions, or edge cases for the **Notes** section — this one's
  free-form (paragraph, bullets, whatever reads clearest), and stays optional.

For an **epic**:
- The overall outcome and problem being solved (the `epic.md` Description).
- The full list of sub-issues, each phrased as the exact title that sub-issue will get —
  these become the `epic.md` Task List items verbatim. Push back on vague task names
  ("styling," "cleanup") — each one needs to be a real, creatable issue title.
- Then, for **each task list item**, interview enough to draft its own feature description
  (see step 3a) — you need the same level of detail per task as you would for a standalone
  feature issue, just gathered in one pass instead of N separate conversations.

## 3. Draft the GitHub issue

Fill in the matching template from `.github/ISSUE_TEMPLATE/` verbatim in structure — same
section headers, same list style — with the interview content. Show the user the drafted
title + body before doing anything else. Delete optional sections (marked "delete this
section if..." in the template) that genuinely don't apply, matching how existing issues in
this repo drop unused sections rather than leaving empty boilerplate.

### 3a. Epics also draft their sub-issues, now

Once the epic body is drafted, immediately draft one sub-issue per Task List item, using
`feature_request.md` — title set to the task's exact text, labels `enhancement, sub-issue`
(matching this repo's existing convention for epic sub-issues, e.g. #41–#49). Show all of
them to the user together with the epic draft before moving on, so they're reviewing the
whole breakdown at once, not issue-by-issue.

## 4. Create the issue(s) (or hand off drafts)

Check what's actually available before promising either path:
- `gh auth status` (via Bash) — if `gh` is installed and authenticated, describe the exact
  action you're about to take (repo, title(s), labels) and get explicit confirmation before
  running `gh issue create`, per CLAUDE.md's GitHub Integration boundary. For an epic,
  create the epic issue first, then each sub-issue; if `gh api` sub-issue linking is
  available and the user wants the sub-issues actually linked under the epic (not just
  cross-referenced by label), offer to do that as a follow-up call once all the numbers
  exist — otherwise note the epic's task list already documents the intended breakdown.
- If `gh` isn't available/authenticated, and no GitHub MCP tool is present either, say so
  plainly and give the user the finished title + body (and, for an epic, every sub-issue's
  title + body) as copyable blocks for them to paste in manually. Ask them to reply with
  the resulting issue number(s) once they exist.

## 5. Root-cause pass (bugs only)

For a bug, before drafting the Spec entry, actually investigate: read the relevant
controller/view/model code and identify the real root cause with file:line references, the
way `Spec/Bugs/BUGS-participant-visibility-in-delete-dropdown.ospec` does. Don't skip this
and paraphrase the user's guess as if it were confirmed.

## 6. Create the Spec entry

Only for bugs and features — not for the epic issue itself (see step 1).

For an epic's generated sub-issues, don't auto-create a Spec entry for every single one —
ask the user first. A styling sub-issue like "Style Event Home Page" may not need one;
a sub-issue with real logic changes probably does. Treat each on its own merits.

1. Build the filename: the category prefix plus a short kebab-case slug of the title, no
   numeric id — e.g. `Spec/Bugs/BUGS-<slug>.ospec` or `Spec/Features/FEATURES-<slug>.ospec`.
   Don't try to match this to the GitHub issue number; per CLAUDE.md's Specs section, the
   two are deliberately not correlated. Check the target folder isn't already using that
   slug for a different item.
2. Fill in the template from `CLAUDE.md`'s Specs section — the `##` heading inside the file
   is just `## <Title>` (no prefix, no id; the `BUGS-`/`FEATURES-` prefix lives only in the
   filename). `### Status` starts as `Proposed.` `### GitHub Issue` is the real `#N` if
   step 4 created it live, otherwise `None yet.`
3. Show the drafted entry to the user before writing the file.
4. If the issue was created via a manual paste-in (step 4's fallback) and the user later
   gives you the issue number, update the `### GitHub Issue` line in the already-written
   file — don't leave it stale.

Never append this entry as a new step inside an existing grouped file (like
`Spec/Refactors/REFACTOR-coding-convention-alignment.ospec`) just because it's the same
category — grouped files are locked to the specific initiative they were created for. This
item always gets its own new file unless the user explicitly says it's a continuation of
that exact initiative.

## Notes

- This skill is explicit instruction to create/modify a Spec file for the item being
  discussed — CLAUDE.md's "wait for explicit instruction" boundary is satisfied by the
  user invoking this skill, but still show drafts before writing, per the Decision-Making
  Boundaries in CLAUDE.md.
- Never invent GitHub access that isn't there — check for `gh` or an MCP tool each time
  rather than assuming last session's availability still holds.
