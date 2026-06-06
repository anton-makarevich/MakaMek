using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

/// <summary>
/// Publishes the truncated water-fall move after fall effects are applied,
/// when <see cref="Mech.CanStandup"/> reflects post-fall state.
/// </summary>
public class WaterFallBroadcastAction(Mech mech, MoveUnitCommand truncatedCommand) : IGameAction
{
    public void Execute(ServerGame game, IList<IGameCommand> commands)
    {
        var canStandup = mech.CanStandup();
        var broadcastCommand = truncatedCommand with
        {
            GameOriginId = game.Id,
            IsCompleted = !canStandup
        };
        commands.Add(broadcastCommand);

        if (canStandup || mech.Position == null) return;
        var completionCommand = truncatedCommand with
        {
            IsCompleted = true,
            MovementPath = MovementPath.CreateSingleSegmentPath(mech.Position, truncatedCommand.MovementType).ToData()
        };
        game.OnMoveUnit(completionCommand);
    }
}
