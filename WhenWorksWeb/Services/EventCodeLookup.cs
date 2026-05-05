using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Data;

namespace WhenWorksWeb.Services;

public sealed class EventCodeLookup : IEventCodeLookup
{
    private readonly ApplicationDbContext _dbContext;

    public EventCodeLookup(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        return _dbContext.Events.AnyAsync(e => e.Code == code, cancellationToken);
    }
}
