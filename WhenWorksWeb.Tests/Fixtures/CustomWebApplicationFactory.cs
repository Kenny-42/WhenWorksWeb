using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WhenWorksWeb.Data;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Tier 3 full-pipeline test harness. Boots the real ASP.NET Core app (routing, middleware, Identity
/// authentication/authorization, antiforgery) via <see cref="WebApplicationFactory{TEntryPoint}"/>, with
/// <see cref="ApplicationDbContext"/> swapped from SQL Server to a real SQLite <c>:memory:</c> database —
/// same rationale and collation-registration requirement as <see cref="SqliteDbContextFixture"/> (see
/// CODING_CONVENTIONS.md's "Collation differs by field on purpose"), just wired through DI service
/// replacement instead of constructing the context directly, since here the whole app builds it.
/// </summary>
/// <remarks>
/// <c>Program</c> is accessible from the test project despite being an implicitly-internal top-level-statement
/// class because of the <c>[InternalsVisibleTo("WhenWorksWeb.Tests")]</c> already added in
/// <c>WhenWorksWeb.csproj</c> for Step 3's testability seam.
/// </remarks>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Kept open for the factory's lifetime — a SQLite in-memory database is destroyed once its connection
    // closes, and the whole point here is for every request within a test to see the same database.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public CustomWebApplicationFactory()
    {
        _connection.Open();
        _connection.CreateCollation("SQL_Latin1_General_CP1_CI_AS", (x, y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase));
        _connection.CreateCollation("SQL_Latin1_General_CP1_CS_AS", (x, y) => string.Compare(x, y, StringComparison.Ordinal));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Program.cs's own AddDbContext<ApplicationDbContext>(UseSqlServer(...)) has already run by this
            // point (its connection string comes from appsettings.json's LocalDB entry, which is never
            // actually connected to — it only needs to be non-null so that line doesn't throw before we get
            // here to remove it).
            //
            // Removing just the DbContextOptions<ApplicationDbContext> descriptor is NOT enough: since EF
            // Core 8, AddDbContext registers each configuration call as a separate, additive
            // IDbContextOptionsConfiguration<ApplicationDbContext> entry (to support composing multiple
            // AddDbContext-style calls together) rather than one replaceable DbContextOptions<TContext>
            // descriptor. Leaving that entry in place means the final composed options apply BOTH
            // UseSqlServer and UseSqlite to the same context, which throws "Services for database providers
            // ... have been registered" — both descriptor types must be removed before re-adding.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));

            using var scopedProvider = services.BuildServiceProvider();
            using var scope = scopedProvider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();

            // Adds TestAuthHandler as an available scheme without replacing Identity's own registration.
            // Only DefaultAuthenticateScheme is redirected to it, so a request only becomes authenticated when
            // it carries the X-Test-UserId header; DefaultChallengeScheme/DefaultForbidScheme stay pointed at
            // Identity's real cookie scheme, so unauthenticated/unauthorized requests still redirect exactly
            // as production does. See TestAuthHandler's remarks for why this is a legitimate test double
            // (standing in for login/cookie mechanics, not for [Authorize] policy evaluation itself).
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultForbidScheme = IdentityConstants.ApplicationScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
