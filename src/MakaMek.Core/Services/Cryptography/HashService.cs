using System.Security.Cryptography;
using System.Text;

namespace Sanet.MakaMek.Core.Services.Cryptography;

public class HashService: IHashService
{
    /// <summary>
    /// Computes a deterministic idempotency key for a command.
    /// The key is based on GameId, PlayerId, UnitId (optional), Phase, Turn, and CommandType.
    /// </summary>
    public Guid ComputeCommandIdempotencyKey(Guid gameId,
        Guid playerId,
        Type commandType,
        int turn,
        string phase,
        Guid? unitId = null,
        int attempt = 0)
    {
        // Build the input string for hashing
        var input = $"{gameId}:{playerId}:{unitId?.ToString() ?? "null"}:{phase}:{turn}:{commandType.Name}:{attempt}";

        // Compute SHA256 hash
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        // Take the first 16 bytes to create a GUID
        return new Guid(hash[..16]);
    }
}