using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Test-only <see cref="SaveChangesInterceptor"/> that lets a test deterministically simulate a concurrent
/// writer landing between a <see cref="Microsoft.EntityFrameworkCore.DbContext"/>'s own uniqueness pre-check
/// and its <c>SaveChangesAsync</c> call, without relying on real multi-threaded timing (which would be flaky).
/// </summary>
/// <remarks>
/// Arm it once via <see cref="ArmOnce"/>; the callback runs exactly once, immediately before the next
/// <c>SaveChangesAsync</c> call is allowed to reach the database, then the interceptor reverts to a no-op. A
/// callback that commits a conflicting row via a second <see cref="ApplicationDbContext"/> on the same
/// connection (see <see cref="SqliteDbContextFixture.CreateConcurrentDbContext"/>) makes the intercepted save
/// fail with a real database-level constraint violation, exactly as it would against a genuinely concurrent
/// request. A callback that simply throws simulates an unrelated save failure instead.
/// </remarks>
public sealed class RaceConditionSaveChangesInterceptor : SaveChangesInterceptor
{
    private Func<Task>? _onSavingAsync;

    /// <summary>
    /// Registers <paramref name="onSavingAsync"/> to run once, the next time <c>SaveChangesAsync</c> is called
    /// on the context this interceptor is attached to.
    /// </summary>
    public void ArmOnce(Func<Task> onSavingAsync) => _onSavingAsync = onSavingAsync;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (_onSavingAsync is { } callback)
        {
            _onSavingAsync = null;
            await callback();
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
