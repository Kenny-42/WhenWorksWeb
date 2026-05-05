namespace WhenWorksWeb.Services;

public interface IEventCodeLookup
{
    Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default);
}
