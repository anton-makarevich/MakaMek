using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;

public interface IWeaponAttackResolver
{
    AttackResolutionData ResolveAttack(
        IUnit attacker,
        IUnit target,
        Weapon weapon,
        WeaponTargetData weaponTargetData,
        IBattleMap battleMap);
}