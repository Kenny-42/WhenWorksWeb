using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Common;
using WhenWorksWeb.Data;

namespace WhenWorksWeb.Services;

/// <summary>
/// Provides functionality to generate unique, human-readable codes that do not already exist in the database.
/// </summary>
/// <param name="dbContext">The database context used to check generated codes for collisions.</param>
public class UniqueCodeGenerator(ApplicationDbContext dbContext)
{
    // The maximum number of attempts to generate a unique code before giving up.
    // This is a safeguard against infinite loops in the unlikely event of many collisions.
    private const int MaxAttempts = 50;

    // Shared source of truth for the code alphabet.
    private static readonly char[] Alphabet = ModelConstants.UniqueCodeAlphabet.ToCharArray();

    /// <summary>
    /// Asynchronously generates a unique event code that does not already exist in the database.
    /// </summary>
    /// <remarks>This method attempts to generate a unique code by checking for collisions in the
    /// database. The number of attempts is limited by an internal maximum. If all attempts result in a collision,
    /// the method throws an exception.</remarks>
    public Task<string> GenerateUniqueEventCodeAsync(CancellationToken cancellationToken = default)
    {
        return GenerateUniqueCodeAsync(
            async code => await dbContext.Events.AnyAsync(e => e.Code == code, cancellationToken));
    }

    /// <summary>
    /// Asynchronously generates a unique participant rejoin code that does not already exist in the database.
    /// </summary>
    /// <remarks>This method uses the exact same generation logic as event codes, with the same length and alphabet.</remarks>
    public Task<string> GenerateUniqueParticipantRejoinCodeAsync(CancellationToken cancellationToken = default)
    {
        return GenerateUniqueCodeAsync(
            async code => await dbContext.Participants.AnyAsync(p => p.RejoinCode == code, cancellationToken));
    }

    /// <summary>
    /// Generates codes until one is found that <paramref name="existsAsync"/> reports as not already in use,
    /// or throws if <see cref="MaxAttempts"/> is exceeded.
    /// </summary>
    /// <param name="existsAsync">Checks whether a candidate code already exists in the database.</param>
    private static async Task<string> GenerateUniqueCodeAsync(Func<string, Task<bool>> existsAsync)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var code = GenerateCode();

            if (!await existsAsync(code))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique code.");
    }

    /// <summary>
    /// Generates a random unique code using the shared alphabet and configured length.
    /// </summary>
    private static string GenerateCode()
    {
        Span<char> chars = stackalloc char[ModelConstants.UniqueCodeLength];

        for (var i = 0; i < ModelConstants.UniqueCodeLength; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
