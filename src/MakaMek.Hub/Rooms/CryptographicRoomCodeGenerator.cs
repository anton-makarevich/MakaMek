using System.Security.Cryptography;

namespace Sanet.MakaMek.Hub.Rooms;

/// <summary>
/// Produces six-character room codes from a readable alphabet using a cryptographic RNG.
/// </summary>
public sealed class CryptographicRoomCodeGenerator : IRoomCodeGenerator
{
    public const int CodeLength = 6;

    // Excludes 0/O and 1/I/L so a spoken or typed code remains unambiguous.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public string Generate()
    {
        var characters = new char[CodeLength];

        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(characters);
    }
}
