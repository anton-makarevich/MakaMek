using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;

public class AttackerPartialCoverGate : IAttackResolutionGate
{
    public bool ShouldSkip(IUnit attacker, IUnit target, Weapon weapon, LocationSlotAssignment primaryAssignment, IBattleMap battleMap, ServerGame game)
    {
        var reversedLosResult = battleMap.GetLineOfSight(
            target.Position!.Coordinates,
            attacker.Position!.Coordinates,
            target.Height,
            attacker.Height);

        var attackerHasPartialCover = game.RulesProvider.HasPartialCover(attacker, reversedLosResult);
        var canBeCovered = game.RulesProvider.CanPartBeCovered(primaryAssignment.Location);

        if (!attackerHasPartialCover || !canBeCovered)
            return false;

        game.Logger.LogInformation(
            "Skipping leg-mounted weapon {WeaponName} attack from {AttackerName} to {TargetName} - attacker has partial cover",
            weapon.Name, attacker.Name, target.Name);
        return true;
    }
}
