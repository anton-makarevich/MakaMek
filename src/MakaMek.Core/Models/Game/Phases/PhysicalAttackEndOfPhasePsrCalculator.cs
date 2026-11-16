using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Runs the piloting skill rolls accumulated during the physical-attack phase (and the resulting
/// falls) once all attacks resolved. Extracted from <see cref="PhysicalAttackResolutionPhase"/>.
/// </summary>
internal sealed class PhysicalAttackEndOfPhasePsrCalculator
{
    private readonly ServerGame _game;
    private readonly PhysicalAttackPhaseState _state;

    public PhysicalAttackEndOfPhasePsrCalculator(ServerGame game, PhysicalAttackPhaseState state)
    {
        _game = game;
        _state = state;
    }

    /// <summary>
    /// One unit at a time in initiative order: the rolls the phase's damage calls for first, then
    /// the rolls the attacks themselves call for. A unit that falls makes no further roll.
    /// </summary>
    public void Calculate()
    {
        var units = _game.InitiativeOrder.SelectMany(p => p.Units);
        foreach (var unit in units)
        {
            if (!_state.AccumulatedDamage.TryGetValue(unit.Id, out var accumulated)) continue;
            if (unit is not Mech { IsDestroyed: false } mech) continue;

            var fallCommands = _game.FallProcessor.ProcessPotentialFall(
                mech,
                _game,
                accumulated.ComponentHits,
                accumulated.DestroyedParts.ToList(),
                accumulated.RollContexts).ToList();

            // A unit makes at most one fall, and makes no further roll once it falls. Apply the
            // successful rolls before it so the actual fall command is not dropped.
            foreach (var fallCommand in fallCommands)
            {
                ApplyFallConsequences(mech, fallCommand);
                if (fallCommand.DamageData is not null) break;
            }
        }

        _state.AccumulatedDamage.Clear();
    }

    /// <summary>
    /// Publishes a fall and the consequences the fall damage calls for: critical hits in the
    /// locations that took structure damage, then the consciousness rolls.
    /// </summary>
    private void ApplyFallConsequences(Mech mech, MechFallCommand fallCommand)
    {
        _game.OnMechFalling(fallCommand);
        _game.CommandPublisher.PublishCommand(fallCommand);
        if (fallCommand.DamageData is null) return;

        var fallStructureDamage = fallCommand.DamageData.HitLocations.HitLocations
            .Where(h => h.Damage.Any(d => d.StructureDamage > 0))
            .SelectMany(h => h.Damage)
            .ToList();
        if (fallStructureDamage.Count != 0)
        {
            var criticalHitsCommand = _game.CriticalHitsCalculator
                .CalculateCriticalHits(mech, fallStructureDamage);
            if (criticalHitsCommand != null)
            {
                criticalHitsCommand.GameOriginId = _game.Id;
                _game.OnCriticalHitsResolution(criticalHitsCommand);
                _game.CommandPublisher.PublishCommand(criticalHitsCommand);
            }
        }

        _game.ProcessConsciousnessRolls(mech);
    }
}
