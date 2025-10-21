namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public interface IClientCommand: IGameCommand
{
    Guid PlayerId { get; init; }

    /// <summary>
    /// Idempotency key for duplicate detection and command tracking.
    /// Computed as a deterministic hash of (GameId, PlayerId, UnitId, Phase, Turn, CommandType).
    /// </summary>
    Guid? IdempotencyKey { get; init; }
}