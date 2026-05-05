namespace WhenWorksWeb.Services;

/// <summary>
/// Provides functionality to generate unique, human-readable event codes that do not already exist in the database.
/// </summary>
public sealed class EventCodeGenerator
{
    private readonly IEventCodeSource _codeSource;
    private readonly IEventCodeLookup _codeLookup;
    private readonly int _maxAttempts;

    public EventCodeGenerator(IEventCodeSource codeSource, IEventCodeLookup codeLookup, int maxAttempts = 50)
    {
        _codeSource = codeSource ?? throw new ArgumentNullException(nameof(codeSource));
        _codeLookup = codeLookup ?? throw new ArgumentNullException(nameof(codeLookup));

        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        _maxAttempts = maxAttempts;
    }

    public async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < _maxAttempts; attempt++)
        {
            var code = _codeSource.GenerateCode();

            if (!await _codeLookup.ExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique event code.");
    }
}
