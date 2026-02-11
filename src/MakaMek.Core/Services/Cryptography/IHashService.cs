namespace Sanet.MakaMek.Core.Services.Cryptography;

public interface IHashService
{
    Guid ComputeCommandIdempotencyKey(
        Guid gameId,
        Guid playerId,
        Type commandType,
        int turn,
        string phase,
        Guid? unitId = null,
        int attempt = 0);
}