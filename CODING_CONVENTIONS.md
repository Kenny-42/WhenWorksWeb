# WhenWorks Coding Conventions

This is the technical reference for writing and refactoring code in this repo — naming, structure, EF Core patterns, domain-specific gotchas, and performance. It's the standard all code is measured against, so treat it as binding, not a style suggestion. For project context, planning process, and collaboration boundaries, see [CLAUDE.md](./CLAUDE.md).

---

## File & Type Organization
- **One type per file**, named to match the type (`EventDate.cs`, `EventSettings.cs`, `EventRole.cs`, `EventMessage.cs`, `UserEventBookmark.cs`). `Event.cs`, `Participant.cs`, and `ApplicationUser.cs` are the exception, each grouping their entity with closely related nested types (e.g. `EventSettings`, `EventDate` inside `Event.cs`) — don't split those further without discussing it first, and don't group unrelated types elsewhere.
- **Namespaces are file-scoped** (`namespace WhenWorksWeb.X;`) everywhere except CLI-generated `Data/Migrations/` files and scaffolded `Areas/Identity` code, which follow their generator's own style — don't hand-edit those to match.

## Controllers vs. Services
This is a deliberate architectural split, not size-based:
- **Services/** is for small, single-purpose, genuinely reusable-across-the-app utility classes — the bar is "its job fits in one sentence and it isn't tied to one controller's feature" (`UniqueCodeGenerator` is the model). It is not a place to relocate business logic just because a controller method got long.
- **Controllers/** is the primary, intentional home for the app's business logic. Long controller files get reorganized *within* `Controllers/`, not moved to `Services/`.
- **Splitting a large controller**: use a **C# partial class**, same class name, spread across multiple files by subject — e.g. `EventsController.cs` (core: create/join), `EventsController.SignIn.cs`, `EventsController.Home.cs`, `EventsController.AccessCookie.cs`. This keeps routes, DI registration, and shared private helpers working with zero behavior change. Do not create separate controller classes with duplicated/rewritten route attributes to achieve the same split — that risks silently changing a URL.

## Comments
This codebase comments more heavily than typical minimal-comment guidance, on purpose — match the existing density, don't lean it out:
- XML doc comments (`///`) on essentially every class, and on **both public and private** methods.
- Inline `//` comments are frequent and often explain *what* the code is doing in addition to *why* — not just non-obvious reasoning.

## Nullable, `required`, and View Models
- `<Nullable>enable</Nullable>` is on — respect it; don't add `!` to silence a real null case.
- Use `required` on properties that must always be set, instead of a constructor parameter or a nullable-with-default (see `Event`, `Participant`).
- View models: `public sealed class XyzViewModel` with `required ... { get; init; }` properties, each with its own one-line XML doc comment (see `MyEventViewModel`, `EventHomeViewModel`). New view models follow this shape, not a plain mutable POCO.

## Constructors
- **Default to primary constructors** (`public class Foo(Dependency dep)`) — most classes here are simple DI containers with no constructor logic, and this is more concise (see `MyEventsController`, `UniqueCodeGenerator`).
- **Use a classic constructor** when the constructor needs to do real work: validate or transform an argument, throw on invalid input, run setup beyond a direct field assignment, or support multiple constructor overloads.
- Judge per class rather than applying one form mechanically; default to primary when in doubt.

## EF Core Patterns
- `.AsNoTracking()` on any query that's read-only within the request (list pages, existence checks, lookups that won't be mutated afterward).
- `ExecuteUpdateAsync` for bulk field updates instead of loading rows and looping (see message-orphaning in `MyEventsController.Delete`).
- Wrap multi-step writes that must succeed or fail together in an explicit `_db.Database.BeginTransactionAsync()` / commit / rollback-on-catch block (see the participant-deletion branch of `MyEventsController.Delete`, `DevelopmentDataSeeder.SeedAsync`).
- Thread `CancellationToken` through every controller action and async method down to the EF calls — don't drop it partway through a call chain.
- Centralize field lengths, regex patterns, and alphabets in `Common/Constants.cs` (`ModelConstants`) — never hardcode a magic number or pattern inline in a new model/validation attribute.
- Project (`.Select(...)`) to only the shape actually needed rather than loading full entities for read-only/display use (see `MyEventsController.Index`).

## String Normalization & Comparison
- Codes (`Event.Code`, `Participant.RejoinCode`) are normalized to **uppercase**; colors are normalized to **lowercase** with a leading `#` stripped. Normalize at the point user input enters the system (the controller action), not scattered later.
- `StringComparison.Ordinal` for IDs/internal keys. `StringComparison.OrdinalIgnoreCase` only for values that are genuinely meant to be case-insensitive to the user (e.g. a rejoin code compared against a case-insensitive DB collation).

## Domain Conventions & Gotchas
Non-obvious rules baked into the schema and controllers. Violating one of these is a likely bug, not a style nitpick:

- **Collation differs by field on purpose.** `Event.Code` / `Participant.RejoinCode` use case-insensitive collation (`SQL_Latin1_General_CP1_CI_AS`); `Participant.DisplayName` uses case-sensitive (`SQL_Latin1_General_CP1_CS_AS`). Don't unify these — codes are meant to be case-insensitive, display names are not.
- **The unique-code alphabet excludes ambiguous characters** (`ModelConstants.UniqueCodeAlphabet = "BCDFGHJKMNPQRSTVWXYZ23456789"`, no `A,E,I,L,O,U,0,1`) so codes are easy to read/share aloud. Event codes and participant rejoin codes both go through `UniqueCodeGenerator` — route any new code-like identifier through it instead of a new random-string scheme.
- **`Participant.DisplayName` and `Color` are each unique per event** (DB unique indexes on `(EventId, DisplayName)` and `(EventId, Color)`), and `DisplayName` has a DB check constraint requiring it pre-trimmed. Check both uniqueness rules in one query (`ValidateParticipantUniquenessAsync`), not two round-trips.
- **`EventMessage.ParticipantId` is nullable and deliberately not cascade-deleted** (`DeleteBehavior.NoAction`, avoiding SQL Server's multiple-cascade-path restriction). Deleting a `Participant` must first null `EventMessages.ParticipantId` via `ExecuteUpdateAsync` (see `MyEventsController.Delete`) — any new participant-deletion path must replicate this or it will throw an FK violation.
- **Guest (non-account) participants authenticate via a signed cookie, not ASP.NET Identity.** `EventsController` issues an `HttpOnly`/`Secure`/`SameSite=Lax` cookie (`WhenWorksWeb.EventAccess.{CODE}`), protected with `IDataProtector`, containing `{code}|{participantId}`. A `CryptographicException` on unprotect means a tampered/stale cookie — catch it and delete the cookie, as the existing code does, rather than letting it bubble up.
- **Rejoin codes are only enforced situationally** — required only when the current session/account doesn't already own the selected participant (`RequiresRejoinCode`). Signed-in owners and the browser's own access cookie bypass it.
- **There is a hardcoded root admin email** (`kenny@mail.com`) checked directly in `ManageUsersModel` (`Areas/Admin`) as the only account allowed to grant/revoke the `Admin` role, and it can't be removed from the role. This is intentional bootstrapping — don't "clean it up" without being asked.
- **`DevelopmentDataSeeder` only runs in `Development`**, only if `Events` is empty, inside one transaction. Add new seed data inside that same guarded method, not a second ad hoc path.

## Views & Frontend
- Bootstrap 5 utility classes directly in markup; HTML comments (`<!-- ... -->`) label page sections, matching the C#-side comment density.
- Client-side behavior is vanilla JS in an IIFE per view (see `Views/MyEvents/Index.cshtml`) — not a shared bundle or framework. Guard on every queried element being non-null before wiring up listeners.
- To pass server data into page JS: serialize with `System.Text.Json.JsonSerializer.Serialize` (camelCase naming policy) into a `data-*` attribute, and `JSON.parse` it back out. Don't hand-build JSON strings or smuggle structured data through hidden form fields.

## Performance
No known performance problems today — this is proactive, so the app doesn't accumulate debt as it grows:
- Project to only the needed shape (`.Select(...)`) instead of materializing full entities for read-only use.
- Avoid N+1: express one query with joins/correlated subqueries (as `MyEventsController.Index` already does) rather than looping and issuing per-row queries.
- Keep LINQ queries deferred/composable until the final `.ToListAsync()`/`.SingleOrDefaultAsync()` — don't materialize early and filter/sort in memory when EF can translate it to SQL.
- No sync-over-async (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`) — async all the way through, consistent with current code.
- New list-style endpoints that could grow unbounded (unlike today's small, per-user `MyEvents` list) should be paginated/limited from the start rather than always returning the full set.
- Add a DB index (`HasIndex` in `OnModelCreating`) for any new column used to filter or sort.
- Prefer `IReadOnlyList<T>` (or arrays) over `List<T>` in public return types / view-model properties, matching `MyEventViewModel`.

## Database & Migrations
- Schema changes require explicit approval (see CLAUDE.md's Decision-Making Boundaries) — this section covers how to execute one once approved.
- Always generate migrations via the EF CLI: `dotnet ef migrations add <Name>` / `dotnet ef migrations remove`.
- **Never** hand-write or hand-edit the contents of a migration file or `ApplicationDbContextModelSnapshot.cs` — those only ever come from the CLI.

## Tooling
- **`.editorconfig`** (repo root) encodes indentation, Allman brace style (matching existing code), and the namespace/primary-constructor preferences above as IDE-level suggestions, not build-breaking rules.
- **Built-in .NET analyzers** are enabled in `WhenWorksWeb.csproj` (`EnableNETAnalyzers`, `EnforceCodeStyleInBuild`) so `dotnet build` / `dotnet format` surface convention violations. These ship with the .NET SDK — no extra package or download required.
- Run `dotnet format` before committing substantial changes to auto-fix formatting-level violations.

## Testing Conventions
- Framework: **xUnit**, in a `WhenWorksWeb.Tests` project (doesn't exist yet — create it with `dotnet new xunit` referencing the main project when we start).
- Test class names mirror the type under test (`EventsControllerTests`). Test method names describe scenario and expected outcome (`SignIn_Post_WithMismatchedRejoinCode_AddsModelError`).
- Whether/when to actually create this project and write tests is a collaboration decision — see CLAUDE.md.
