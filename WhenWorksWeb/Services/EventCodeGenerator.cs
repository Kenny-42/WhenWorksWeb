using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using WhenWorksWeb.Common;
using WhenWorksWeb.Data;

namespace WhenWorksWeb.Services
{
    /// <summary>
    /// Provides functionality to generate unique, human-readable event codes that do not already exist in the database.
    /// </summary>
    public class EventCodeGenerator
    {
        // The length of the event code.
        private const int CodeLength = ModelConstants.EventCodeLength;

        // The maximum number of attempts to generate a unique code before giving up.
        // This is a safeguard against infinite loops in the unlikely event of many collisions.
        private const int MaxAttempts = 50;

        // Shared source of truth for the event code alphabet.
        private static readonly char[] Alphabet = ModelConstants.EventCodeAlphabet.ToCharArray();

        private readonly ApplicationDbContext _dbContext;

        public EventCodeGenerator(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Asynchronously generates a unique event code that does not already exist in the database.
        /// </summary>
        /// <remarks>This method attempts to generate a unique code by checking for collisions in the
        /// database. The number of attempts is limited by an internal maximum. If all attempts result in a collision,
        /// the method throws an exception.</remarks>
        public async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken = default)
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var code = GenerateCode();

                bool exists = await _dbContext.Events
                    .AnyAsync(e => e.Code == code, cancellationToken);

                if (!exists)
                {
                    return code;
                }
            }

            throw new InvalidOperationException("Unable to generate a unique event code.");
        }

        /// <summary>
        /// Generates a random event code using the shared alphabet and configured length.
        /// </summary>
        private static string GenerateCode()
        {
            Span<char> chars = stackalloc char[CodeLength];

            for (var i = 0; i < CodeLength; i++)
            {
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }

            return new string(chars);
        }
    }
}