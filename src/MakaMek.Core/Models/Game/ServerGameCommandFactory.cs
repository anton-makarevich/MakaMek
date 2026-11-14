using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Builds the server-originated commands so <see cref="ServerGame"/> does not reference every
/// individual command payload type.
/// </summary>
internal static class ServerGameCommandFactory
{
    public static SetBattleMapCommand SetBattleMap(Guid gameId, IBattleMap map) =>
        new()
        {
            GameOriginId = gameId,
            MapData = map.ToData()
        };

    public static GameEndedCommand GameEnded(Guid gameId, GameEndReason reason, Guid? playerId) =>
        new()
        {
            GameOriginId = gameId,
            Reason = reason,
            Timestamp = DateTime.UtcNow,
            PlayerId = playerId
        };

    public static ChangeActivePlayerCommand ActivePlayer(Guid gameId, IPlayer player, int unitsToMove) =>
        new()
        {
            GameOriginId = gameId,
            PlayerId = player.Id,
            UnitsToPlay = unitsToMove
        };

    public static ChangePhaseCommand Phase(Guid gameId, PhaseNames phase) =>
        new()
        {
            GameOriginId = gameId,
            Phase = phase
        };

    public static TurnIncrementedCommand TurnIncremented(Guid gameId, int turn) =>
        new()
        {
            GameOriginId = gameId,
            TurnNumber = turn
        };
}
