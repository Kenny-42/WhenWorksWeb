# Project: WhenWorks Web Application

## Overview
WhenWorks is an ASP.NET Core MVC web application that helps groups coordinate availability for events. Users can create or join events, select a display name and color, view joined events, and interact with event-specific features. The project is actively evolving; architectural decisions are made collaboratively with the developer, not unilaterally.

## How to use these docs
This file covers project context, planning process, and the boundaries Claude Code operates within. For the actual rules on how to write and refactor code — file/type organization, controllers vs. services, EF Core patterns, domain-specific gotchas, performance, tooling — see **[CODING_CONVENTIONS.md](./CODING_CONVENTIONS.md)**. Read both before writing or refactoring code; CODING_CONVENTIONS.md is the standard all code in this repo is measured against.

---

## Tech Stack
- .NET 10, ASP.NET Core MVC (plus Razor Pages for Identity and the Admin area)
- Entity Framework Core (code-first), primary provider SQL Server
- ASP.NET Core Identity (default scaffolding + a custom `ApplicationUser`)
- Bootstrap 5 + minimal custom CSS, vanilla JS (no frontend framework)

## Build & Run
```
dotnet build
dotnet run
dotnet test   # WhenWorksWeb.Tests (xUnit) doesn't exist yet — see Testing below
```

## Project Structure
```
Areas/Admin/    Razor Pages, [Authorize(Roles="Admin")], manages the Admin role
Areas/Identity/ Scaffolded Identity UI — don't modify without approval
Common/         ModelConstants — single source of truth for lengths/patterns/alphabets
Controllers/    Nearly all business logic lives here — intentional, see CODING_CONVENTIONS.md.
                Large controllers split into partial-class files by subject (e.g. `EventsController.SignIn.cs`).
Data/           ApplicationDbContext, Migrations/ (CLI-generated only), Seed/ (dev-only sample data)
Models/         EF entities AND view models together — no separate ViewModels/ folder
Services/       Small, single-purpose, reusable utilities (e.g. UniqueCodeGenerator)
Views/          Razor views (Home, Events, MyEvents, Shared)
wwwroot/        css/site.css, js/site.js
```

---

## Architecture & Roadmap
Current flow: `Home/Index` (create/join by code) → `EventsController.Create`/`Join` → `EventsController.SignIn` → `EventsController.Home` (landing page). `MyEventsController` lists/deletes events and participants.

**Modeled but not built yet:**
- Availability system — `EventDate` (candidate dates) exists in the schema with no voting UI/logic yet.
- Chat system — `EventMessage` has a full data model with no controller actions/views yet; the event home page is currently just a confirmation screen.

Both should be designed collaboratively (discuss the approach together) before implementation begins, not built ahead of that conversation.

---

## Specs (Bug & Feature Planning)
`Spec/bugfixes.ospec` is the active spec convention — append new entries using this template (see the existing BUG-52 entry):
```
## BUG-<number>: <short title>
### Status / GitHub Issue / Summary / Reported Behavior / Expected Behavior
### Root Cause / Proposed Fix / Acceptance Criteria / Out of Scope
```
Wait for explicit instruction before creating or modifying a Spec file — don't add a bugfix entry unprompted just because a bug came up in conversation. There's no separate `architecture.ospec`/`features.ospec`/`api-design.ospec` yet — that content now lives in these two docs and can be split out later if needed.

## Testing
- Framework and structure are decided (xUnit, `WhenWorksWeb.Tests` — see CODING_CONVENTIONS.md).
- Claude may help create the test project and propose coverage improvements, but must not add or modify tests unless explicitly requested.

## Database & Schema
- Schema changes require an explicit request — don't add/change EF model configuration speculatively.
- Execution rules (CLI-only, never hand-edit migrations) are in CODING_CONVENTIONS.md.

---

## GitHub Integration (MCP) & Git Conventions
Claude Code may use the GitHub MCP server to browse files, inspect PRs, read issues, suggest issue creation, and review Actions logs — but must never push commits, create branches, or open PRs without explicit approval, and must always describe a proposed GitHub action before performing it.

Commit/PR titles follow the existing history's pattern: `Issue#<N> <description> (#<PR>)` when tied to a tracked issue (e.g. `Issue#38 add delete workflow to my events (#39)`).

---

## Decision-Making Boundaries
Claude Code must:
- Always ask for confirmation before structural or architectural changes; never act autonomously; offer suggestions and tradeoffs rather than unilateral decisions.
- Propose improvements but **never implement changes without explicit approval**.
- Not modify Identity scaffolding (`Areas/Identity`) without explicit approval.
- Not generate deployment scripts or Azure configuration unless asked.
- **Before suggesting or adding any new tool, package, extension, or framework, explain what it is and why it's actually needed** — never introduce one silently or assume familiarity.

---

## Summary
WhenWorks requires Claude Code to act as a collaborative assistant, not an autonomous agent. All major architectural, database, Identity, or feature decisions are approved by the developer first. Code itself should follow CODING_CONVENTIONS.md exactly — that document is intentionally decisive, not a set of loose defaults, so the codebase stays consistent as it grows.
