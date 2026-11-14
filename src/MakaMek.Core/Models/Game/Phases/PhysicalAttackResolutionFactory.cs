using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Builds the resolution payload/command for a physical attack that produced no roll, such as a
/// charge whose target could not be selected.
/// </summary>
internal static class PhysicalAttackResolutionFactory
{
    /// <summary>
    /// The automatic failure of a charge: no roll, no damage and no piloting skill roll.
    /// </summary>
    public static PhysicalAttackResolutionCommand AutoFailCharge(Guid gameId, ChargeQueueItem item) =>
        new()
        {
            ResolutionId = Guid.NewGuid(),
            GameOriginId = gameId,
            PlayerId = item.Player.Id,
            AttackerId = item.Attacker.Id,
            TargetId = item.DeclaredTargetId,
            ResolutionData = new PhysicalAttackResolutionData(
                PhysicalAttackType.Charge,
                null,
                0,
                new AttackResolutionData(0, [], false, HitDirection.Front, 0),
                [],
                new ChargeResolutionData(
                    ChargeOutcome.AutoFail,
                    item.HexesMoved,
                    false,
                    item.DeclaredTargetId))
        };
}


