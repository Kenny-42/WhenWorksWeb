using System.Security.Cryptography;
using WhenWorksWeb.Common;

namespace WhenWorksWeb.Services;

public sealed class RandomEventCodeSource : IEventCodeSource
{
    private static readonly char[] Alphabet = ModelConstants.EventCodeAlphabet.ToCharArray();

    public string GenerateCode()
    {
        Span<char> chars = stackalloc char[ModelConstants.EventCodeLength];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
