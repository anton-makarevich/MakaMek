using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;

public class AttackerPartialCoverGate : IAttackResolutionGate
{
    public bool ShouldSkip(IUnit attacker, IUnit target, Weapon weapon, LocationSlotAssignment primaryAssignment, IBattleMap battleMap, IRulesProvider rulesProvider, ILogger logger)
    {
        var reversedLosResult = battleMap.GetLineOfSight(
            target.Position!.Coordinates,
            attacker.Position!.Coordinates,
            target.Height,
            attacker.Height,
            target.Position.Surface,
            attacker.Position.Surface);

        var attackerHasPartialCover = rulesProvider.HasPartialCover(attacker, reversedLosResult);
        var canBeCovered = rulesProvider.CanPartBeCovered(primaryAssignment.Location);

        if (!attackerHasPartialCover || !canBeCovered)
            return false;

        logger.LogInformation(
            "Skipping weapon at {Location} attack from {AttackerName} to {TargetName} - attacker has partial cover",
            primaryAssignment.Location, attacker.Name, target.Name);
        return true;
    }
}
