using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Runs the piloting skill rolls accumulated during the weapon attack resolution phase (and the
/// resulting falls) once all attacks resolved. Extracted from
/// <see cref="WeaponAttackResolutionPhase"/>.
/// </summary>
internal sealed class WeaponAttackEndOfPhasePsrCalculator(
    ServerGame game,
    WeaponAttackResolutionState state)
{
    public void Calculate()
    {
        foreach (var (unitId, accumulatedDamage) in state.AccumulatedDamage)
        {
            var unit = game.Players
                .SelectMany(p => p.Units)
                .FirstOrDefault(u => u.Id == unitId);

            if (unit is not Mech targetMech) continue;

            CalculateForMech(targetMech, accumulatedDamage);
        }

        state.Clear();
    }

    private void CalculateForMech(Mech mech, WeaponAttackAccumulatedDamage accumulatedDamage)
    {
        // Calculate PSRs using the accumulated damage data from the entire phase
        var mechFallingCommands = game.FallProcessor.ProcessPotentialFall(
            mech,
            game,
            accumulatedDamage.AllComponentHits,
            accumulatedDamage.AllDestroyedParts);

        foreach (var fallingCommand in mechFallingCommands)
        {
            game.OnMechFalling(fallingCommand);
            game.CommandPublisher.PublishCommand(fallingCommand);
            if (fallingCommand.DamageData is null) break;

            ApplyFallStructuralDamage(mech, fallingCommand);

            // Process consciousness rolls for pilot damage accumulated during this phase
            ConsciousnessRolls.Process(game, mech);
            break;
        }
    }

    private void ApplyFallStructuralDamage(Mech mech, MechFallCommand fallCommand)
    {
        var locationsWithDamagedStructure = fallCommand.DamageData!.HitLocations.HitLocations
            .Where(h => h.Damage.Any(d => d.StructureDamage > 0))
            .SelectMany(h => h.Damage)
            .ToList();
        if (locationsWithDamagedStructure.Count == 0) return;

        var fallCriticalHitsCommand = game.CriticalHitsCalculator
            .CalculateCriticalHits(mech, locationsWithDamagedStructure);
        if (fallCriticalHitsCommand == null) return;

        fallCriticalHitsCommand.GameOriginId = game.Id;
        game.OnCriticalHitsResolution(fallCriticalHitsCommand);
        game.CommandPublisher.PublishCommand(fallCriticalHitsCommand);
    }
}
