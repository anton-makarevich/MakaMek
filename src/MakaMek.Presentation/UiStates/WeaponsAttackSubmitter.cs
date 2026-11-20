using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Builds and publishes the weapon-attack declaration (or the skip/no-targets declaration) for the
/// current attacker. Exposed as an extension on the client game so <see cref="WeaponsAttackState"/>
/// does not depend on the command payload.
/// </summary>
public static class ClientGameWeaponsAttackExtensions
{
    extension(IClientGame game)
    {
        /// <summary>
        /// Creates and sends the declaration for the attacker's weapon targets.
        /// </summary>
        public void SubmitWeaponAttack(
            IUnit attacker,
            Func<Weapon, PartLocation?> getAimedShotTarget)
        {
            // Create a weapon target data list
            var weaponTargetsData = new List<WeaponTargetData>();

            // Only process weapon targets if there are any (otherwise this is a Skip Attack)
            if (attacker.WeaponAttackState.SelectedWeapons.Any())
            {
                var primaryTarget = attacker.WeaponAttackState.PrimaryTarget;
                foreach (var (weapon, target) in attacker.WeaponAttackState.WeaponTargets)
                {
                    var isPrimaryTarget = target == primaryTarget;

                    weaponTargetsData.Add(new WeaponTargetData
                    {
                        Weapon = weapon.ToData(),
                        TargetId = target.Id,
                        IsPrimaryTarget = isPrimaryTarget,
                        AimedShotTarget = getAimedShotTarget(weapon)
                    });
                }
            }

            // Create and send the command
            var command = new WeaponAttackDeclarationCommand
            {
                GameOriginId = game.Id,
                PlayerId = game.PhaseStepState!.Value.ActivePlayer.Id,
                UnitId = attacker.Id,
                WeaponTargets = weaponTargetsData
            };

            game.DeclareWeaponAttack(command);
        }
    }
}
