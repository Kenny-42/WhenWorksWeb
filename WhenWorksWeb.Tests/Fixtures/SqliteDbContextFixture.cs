using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Data;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Base class for tests that need a real, isolated <see cref="ApplicationDbContext"/> backed by SQLite
/// <c>:memory:</c> rather than the EF Core InMemory provider (see CODING_CONVENTIONS.md's Testing
/// Conventions for why: InMemory silently ignores relational/constraint bugs a real database would catch).
/// </summary>
/// <remarks>
/// xUnit constructs a new instance of the test class for every <c>[Fact]</c>/<c>[Theory]</c> method, so
/// inheriting from this base class gives each test method its own SQLite connection and freshly created
/// schema — state never leaks between tests, with no shared fixture/reset step required.
/// </remarks>
public abstract class SqliteDbContextFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// A real <see cref="ApplicationDbContext"/> backed by a fresh, empty SQLite <c>:memory:</c> database.
    /// </summary>
    protected ApplicationDbContext Db { get; }

    protected SqliteDbContextFixture()
    {
        // A SQLite in-memory database only exists for as long as its connection stays open, so the
        // connection must be opened here and kept alive for the lifetime of this fixture (not created
        // fresh per-query the way UseSqlite("DataSource=:memory:") would, which would give every query
        // a different, empty database).
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // ApplicationDbContext.OnModelCreating pins specific SQL Server collations on Event.Code,
        // Participant.DisplayName, and Participant.RejoinCode (see CODING_CONVENTIONS.md's "Collation
        // differs by field on purpose"). SQLite has no built-in collation with those names, so schema
        // creation fails outright (SqliteException: "no such collation sequence") unless equivalent named
        // collations are registered on the connection first. This is the standard, documented mechanism
        // for this exact cross-provider scenario (Microsoft.Data.Sqlite's SqliteConnection.CreateCollation)
        // — not a workaround. The registered comparisons mirror the real collations' actual semantics
        // (case-insensitive vs. case-sensitive ordinal), so uniqueness/index behavior in tests matches
        // production for this app's actual data (ASCII-only event codes and display names). They are an
        // approximation, not a byte-for-byte reproduction of SQL Server's linguistic collation rules —
        // that distinction would only matter for accented/non-ASCII input, which is out of scope here.
        _connection.CreateCollation("SQL_Latin1_General_CP1_CI_AS", (x, y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase));
        _connection.CreateCollation("SQL_Latin1_General_CP1_CS_AS", (x, y) => string.Compare(x, y, StringComparison.Ordinal));

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new ApplicationDbContext(options);

        // EnsureCreated (not migrations) builds the schema directly from the current model — appropriate
        // here since tests want the current schema, not a replay of migration history. EF Core's Sqlite
        // provider enables `PRAGMA foreign_keys = ON` automatically for connections it manages, including
        // this pre-opened one, so foreign key violations are enforced exactly as they would be in
        // production (verified: a dangling FK insert throws DbUpdateException, not silently succeeds).
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
