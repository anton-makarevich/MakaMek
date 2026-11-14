using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Applies one resolved weapon attack: damage, external heat, the resolution command, hull breach,
/// critical hits and the consciousness rolls, then records the damage for the end-of-phase PSRs.
/// Extracted from <see cref="WeaponAttackResolutionPhase"/>.
/// </summary>
internal sealed class WeaponAttackApplier(ServerGame game, WeaponAttackResolutionState state)
{
    private readonly record struct CriticalHitsOutcome(
        IReadOnlyList<ComponentHitData> ComponentHits,
        IReadOnlyList<PartLocation> BlownOffParts);

    public void Apply(
        IPlayer player,
        IUnit attacker,
        Weapon weapon,
        IUnit target,
        AttackResolutionData resolution)
    {
        // Track destroyed parts before damage
        var destroyedPartsBefore = GetDestroyedParts(target);
        var wasDestroyedBefore = target.IsDestroyed;

        ApplyDamage(target, weapon, resolution);

        // Check which parts are newly destroyed and whether the unit was destroyed by this attack
        var newlyDestroyedParts = GetDestroyedParts(target).Except(destroyedPartsBefore).ToList();
        var unitNewlyDestroyed = !wasDestroyedBefore && target.IsDestroyed;

        resolution = resolution with
        {
            DestroyedParts = newlyDestroyedParts.Count != 0 ? newlyDestroyedParts : null,
            UnitDestroyed = unitNewlyDestroyed
        };

        PublishResolution(player, attacker, weapon, target, resolution);

        ApplyHullBreach(target, resolution);
        var criticalHits = ApplyCriticalHits(target, resolution);

        // Process consciousness rolls for pilot damage from the attack and critical hits. This must
        // happen after both resolution commands are published to keep the game log in order.
        ConsciousnessRolls.Process(game, target);

        var allDestroyedParts = (resolution.DestroyedParts ?? []).Concat(criticalHits.BlownOffParts).Distinct();
        state.Accumulate(target.Id, criticalHits.ComponentHits, allDestroyedParts);
    }

    private static List<PartLocation> GetDestroyedParts(IUnit unit) =>
        unit.Parts.Values
            .Where(p => p.IsDestroyed).Select(p => p.Location).ToList();

    private static void ApplyDamage(IUnit target, Weapon weapon, AttackResolutionData resolution)
    {
        if (resolution is not { IsHit: true, HitLocationsData.HitLocations: not null }) return;

        target.ApplyDamage(resolution.HitLocationsData.HitLocations, resolution.AttackDirection);

        // Apply external heat if the weapon has ExternalHeat property
        if (weapon.ExternalHeat > 0
            && resolution.HitLocationsData.HitLocations.Any(h => h.CoveringHexAbsorption == null))
        {
            target.AddExternalHeat(weapon.Name, weapon.ExternalHeat);
        }
    }

    private void PublishResolution(
        IPlayer player,
        IUnit attacker,
        Weapon weapon,
        IUnit target,
        AttackResolutionData resolution)
    {
        var command = new WeaponAttackResolutionCommand
        {
            ResolutionId = Guid.NewGuid(),
            GameOriginId = game.Id,
            PlayerId = player.Id,
            AttackerId = attacker.Id,
            WeaponData = weapon.ToData(),
            TargetId = target.Id,
            ResolutionData = resolution
        };

        attacker.FireWeapon(command.WeaponData);

        game.CommandPublisher.PublishCommand(command);
    }

    /// <summary>
    /// Calculates and sends the hull breach if the target was submerged and took damage.
    /// </summary>
    private void ApplyHullBreach(IUnit target, AttackResolutionData resolution)
    {
        var hitLocations = resolution.HitLocationsData?.HitLocations;
        if (hitLocations == null || !hitLocations.Any(h => h.Damage.Any(HasAnyDamage))) return;

        var hullBreachCommand = game.HullBreachCalculator
            .CalculateAndApplyHullBreach(target, hitLocations.SelectMany(h => h.Damage).ToList());

        if (hullBreachCommand == null) return;

        hullBreachCommand.GameOriginId = game.Id;
        game.CommandPublisher.PublishCommand(hullBreachCommand);
    }

    /// <summary>
    /// Calculates and sends the critical hits if any location received structure damage, and
    /// reports the component hits and blown-off parts for the end-of-phase PSRs.
    /// </summary>
    private CriticalHitsOutcome ApplyCriticalHits(IUnit target, AttackResolutionData resolution)
    {
        var hitLocations = resolution.HitLocationsData?.HitLocations;
        if (hitLocations == null || !hitLocations.Any(h => h.Damage.Any(d => d.StructureDamage > 0)))
            return new CriticalHitsOutcome([], []);

        var criticalHitsCommand = game.CriticalHitsCalculator
            .CalculateCriticalHits(target, hitLocations.SelectMany(h => h.Damage).ToList());

        if (criticalHitsCommand == null) return new CriticalHitsOutcome([], []);

        criticalHitsCommand.GameOriginId = game.Id;
        // The server transport does not loop commands back to the server game.
        // Apply the authoritative result before publishing it to clients.
        game.OnCriticalHitsResolution(criticalHitsCommand);
        game.CommandPublisher.PublishCommand(criticalHitsCommand);

        // Component hits can cause a fall; blown-off parts (leg/arm/head) are PSR candidates too.
        return new CriticalHitsOutcome(
            criticalHitsCommand.CriticalHits.SelectMany(ch => ch.HitComponents ?? []).ToList(),
            criticalHitsCommand.CriticalHits.Where(ch => ch.IsBlownOff).Select(ch => ch.Location).ToList());
    }

    private static bool HasAnyDamage(LocationDamageData damage) =>
        damage.ArmorDamage > 0 || damage.StructureDamage > 0;
}
