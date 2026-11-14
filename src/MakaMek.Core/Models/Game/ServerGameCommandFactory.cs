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
    extension(ServerGame game)
    {
        public SetBattleMapCommand CreateSetBattleMapCommand(IBattleMap map) =>
            new()
            {
                GameOriginId = game.Id,
                MapData = map.ToData()
            };

        public GameEndedCommand CreateGameEndedCommand(GameEndReason reason, Guid? playerId) =>
            new()
            {
                GameOriginId = game.Id,
                Reason = reason,
                Timestamp = DateTime.UtcNow,
                PlayerId = playerId
            };

        public ChangeActivePlayerCommand CreateChangeActivePlayerCommand(IPlayer player, int unitsToMove) =>
            new()
            {
                GameOriginId = game.Id,
                PlayerId = player.Id,
                UnitsToPlay = unitsToMove
            };

        public ChangePhaseCommand CreateChangePhaseCommand(PhaseNames phase) =>
            new()
            {
                GameOriginId = game.Id,
                Phase = phase
            };

        public TurnIncrementedCommand CreateTurnIncrementedCommand(int turn) =>
            new()
            {
                GameOriginId = game.Id,
                TurnNumber = turn
            };
    }
}
