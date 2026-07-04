using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;

public interface IAttackResolutionGate
{
    bool ShouldSkip(IUnit attacker, IUnit target, Weapon weapon, LocationSlotAssignment primaryAssignment, IBattleMap battleMap, IRulesProvider rulesProvider, ILogger logger);
}
