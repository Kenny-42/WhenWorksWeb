# Project: WhenWorks Web Application

## Overview
WhenWorks is an ASP.NET Core MVC web application that helps groups coordinate availability for events. Users can create or join events, select a display name and color, view joined events, and interact with event-specific features. The project is actively evolving, and architectural decisions should always be made collaboratively with the developer.

---

## Tech Stack
- .NET 10
- ASP.NET Core MVC
- C#
- Entity Framework Core (code-first)
- SQL Server
- Identity Framework (default scaffolding + custom user model)
- Bootstrap + custom CSS
- GitHub for version control

---

## Build & Run Commands
commands:
  build: "dotnet build"
  run: "dotnet run"
  test: "dotnet test"   # Only when tests exist

---

## Project Structure
src: "./"

backend:
  controllers: "./Controllers"
  models: "./Models"
  services: "./Services"
  data: "./Data"
  identity: "./Areas/Identity"

frontend:
  static: "./wwwroot"
  views: "./Views"

notes:
  - Controllers currently contain most business logic.
  - Services folder is used for small, isolated functionality (e.g., UniqueCodeGenerator).
  - Future refactors may move some controller logic into services, but only with explicit approval.

---

## Permissions
allow:
  - read
  - write
  - edit
  - bash

deny:
  - autonomous architectural changes
  - modifying Identity files without explicit approval
  - creating or modifying database schema unless requested

---

## Development Workflow Expectations
Claude Code must:
- Propose improvements but **never implement changes without explicit approval**.
- Follow MVC conventions and existing naming patterns.
- Maintain compatibility with Bootstrap and the existing stylesheet.
- Respect the current controller-heavy architecture unless instructed otherwise.
- Use EF Core code-first patterns consistent with ApplicationDbContext.
- Avoid modifying Identity scaffolding unless explicitly told to.
- Avoid generating deployment scripts or Azure configuration unless asked.
- Use GitHub workflows appropriately when interacting with MCP (issues, PRs, etc.).

---

## GitHub Integration (MCP)
Claude Code may use the GitHub MCP server to:
- Browse repository files
- Inspect pull requests
- Read issues
- Suggest issue creation
- Review GitHub Actions logs

Claude Code must:
- Never push commits, create branches, or open PRs without explicit approval.
- Always describe proposed GitHub actions before performing them.

---

## Specs (OpenSpec Integration)
Spec:
  - Spec/architecture.ospec
  - Spec/features.ospec
  - Spec/api-design.ospec

Guidance:
- Specs should be used when planning new features or refactoring major components.
- Availability system and chat system should be designed collaboratively using OpenSpec when the time comes.
- Claude Code should wait for explicit instruction before generating or modifying Spec files.

---

## Feature Development Guidelines

### Services Layer
- Controllers currently contain most logic.
- Services are used for small, isolated functionality.
- Claude Code may suggest service extraction when controllers become too large, but must not implement without approval.

### Unit Testing -- edit
- Claude Code should help locate or generate a test project when asked.
- Claude Code may propose test coverage improvements.
- Claude Code must not create or modify tests unless explicitly requested.

---

## UI/Frontend Guidelines
Claude Code should:
- Generate Bootstrap-compatible HTML.
- Use existing CSS conventions.
- Maintain responsiveness and cross-device compatibility.
- Avoid introducing new frontend frameworks unless explicitly approved.

---

## Decision-Making Boundaries
Claude Code must:
- Always ask for confirmation before making structural changes.
- Never act autonomously.
- Provide suggestions, options, and tradeoffs rather than unilateral decisions.

---

## Summary
This project requires Claude Code to act as a collaborative assistant, not an autonomous agent. All major architectural, database, Identity, or feature decisions must be approved by the developer. Claude Code should enhance productivity, generate code when asked, help with planning, and support GitHub workflows — always under explicit direction.

