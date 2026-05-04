using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Data;

namespace WhenWorksWeb.Services
{
    /// <summary>
    /// Provides functionality to generate unique, human-readable event codes that do not already exist in the database.
    /// </summary>
    public class EventCodeGenerator
    {
        // The length of the event code.
        private const int CodeLength = 6;

        // The maximum number of attempts to generate a unique code before giving up.
        // This is a safeguard against infinite loops in the unlikely event of many collisions.
        private const int MaxAttempts = 50;

        // A custom alphabet that excludes easily confused characters (A, E, I, L, O, U, 0, 1) to improve readability of codes.
        private static readonly char[] Alphabet = "BCDFGHJKMNPQRSTVWXYZ23456789".ToCharArray();

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

                Boolean exists = await _dbContext.Events
                    .AnyAsync(e => e.Code == code, cancellationToken);

                if (!exists)
                {
                    return code;
                }
            }

            throw new InvalidOperationException("Unable to generate a unique event code.");
        }

        /// <summary>
        /// Generates a random 6 character code using a predefined alphabet.
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