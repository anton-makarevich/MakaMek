using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

/// <summary>
/// Resolves the fall of every 'Mech standing on a collapsing bridge deck. Each fall is resolved at
/// the bridge hex, not at the pre-move position, and must always happen.
/// </summary>
internal sealed class BridgeCollapseFallResolver
{
    private const string FallRequiredMessage = "Bridge collapse should always result in a fall for unit {UnitId}";

    public void AppendFallActions(
        MovementInterruptContext context,
        List<IGameAction> actions,
        HexCoordinates bridgeCoords,
        int bridgeHeight,
        List<IUnit> unitsOnHex)
    {
        foreach (var hexUnit in unitsOnHex)
        {
            if (hexUnit is not Mech hexMech) continue;

            var movementType = hexUnit.Id == context.Unit.Id
                ? context.MoveCommand.MovementType
                : hexMech.MovementTaken?.MovementType ?? MovementType.StandingStill;

            var fallContextData = context.Game.FallProcessor.ProcessMovementAttempt(
                hexMech, new BridgeCollapseRollContext(bridgeHeight), context.Game, movementType,
                new HexPosition(bridgeCoords, hexMech.Position!.Facing));

            if (!fallContextData.IsFalling)
            {
                context.Game.Logger.LogError(FallRequiredMessage, hexMech.Id);
                throw new InvalidOperationException(
                    $"Bridge collapse should always result in a fall for unit {hexMech.Id}");
            }

            actions.Add(new ApplyFallAction(hexMech, fallContextData.ToMechFallCommand()));
        }
    }
}
