using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WhenWorksWeb.Data;
using WhenWorksWeb.Models;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Builds a real <see cref="UserManager{TUser}"/> backed by a real EF Core-based <see cref="IUserStore{TUser}"/>
/// over a test <see cref="ApplicationDbContext"/>, instead of mocking <see cref="UserManager{TUser}"/>.
/// Controllers call <c>UserManager.GetUserAsync(User)</c>/<c>CreateAsync(...)</c>, and this exercises the real
/// ASP.NET Core Identity machinery those calls go through in production, consistent with this project's "test
/// through the real thing, not a re-implementation" testing convention.
/// </summary>
/// <remarks>
/// Doesn't construct <c>new UserStore&lt;ApplicationUser&gt;(db)</c> directly — <c>ApplicationUser</c>'s
/// <c>required</c> members mean it can't satisfy <c>UserStore&lt;TUser&gt;</c>'s compile-time <c>new()</c>
/// constraint. Production code never hits this because Identity's <c>AddEntityFrameworkStores</c> constructs
/// the store via runtime reflection (<c>Type.MakeGenericType</c>), not a compile-time generic instantiation —
/// so this factory builds a small real DI container using the same <c>AddIdentityCore</c> +
/// <c>AddEntityFrameworkStores</c> registration Program.cs uses, and resolves <see cref="UserManager{TUser}"/>
/// from it, rather than trying to work around the constraint by hand.
/// </remarks>
public static class TestUserManagerFactory
{
    /// <summary>
    /// Creates a <see cref="UserManager{TUser}"/> for <see cref="ApplicationUser"/> backed by <paramref name="db"/>.
    /// </summary>
    public static UserManager<ApplicationUser> Create(ApplicationDbContext db)
    {
        var services = new ServiceCollection();

        // Register the caller's exact ApplicationDbContext instance so the resolved store reads/writes through
        // the same SQLite connection the test's fixture owns, rather than a separately configured one.
        services.AddSingleton(db);
        services.AddLogging();

        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        return services.BuildServiceProvider().GetRequiredService<UserManager<ApplicationUser>>();
    }
}
