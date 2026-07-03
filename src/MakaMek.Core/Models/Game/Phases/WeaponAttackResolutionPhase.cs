using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class WeaponAttackResolutionPhase(ServerGame game) : GamePhase(game)
{
    private readonly IWeaponAttackResolver _weaponAttackResolver = new WeaponAttackResolver(
        game.RulesProvider,
        game.DiceRoller,
        game.DamageTransferCalculator,
        game.ToHitCalculator);

    // List of players in initiative order for attack resolution
    private List<IPlayer> _playersInOrder = [];
    
    // Units with weapons that have targets, organized by player
    private readonly Dictionary<Guid, List<IUnit>> _unitsWithTargets = new();
    
    // Dictionary to track accumulated damage data for PSR calculations at the phase end
    private readonly Dictionary<Guid, UnitPhaseAccumulatedDamage> _accumulatedDamageData = new();

    private readonly record struct AttackQueueItem(
        IPlayer Player,
        IUnit Attacker,
        Weapon? Weapon,
        IUnit? TargetUnit,
        WeaponTargetData WeaponTargetData
    );

    public override void Enter()
    {
        // Clear any accumulated damage data from previous phases
        _accumulatedDamageData.Clear();
        
        // Initialize the attack resolution process
        _playersInOrder = Game.InitiativeOrder.ToList();
        
        // Prepare the dictionary of units with targets for each player
        PrepareUnitsWithTargets();
        
        // Start resolving attacks
        ResolveNextAttack();
    }
    
    private void PrepareUnitsWithTargets()
    {
        _unitsWithTargets.Clear();

        foreach (var player in _playersInOrder)
        {
            var unitsWithWeaponTargets = player.Units
                .Where(unit => unit is { CanFireWeapons: true, HasDeclaredWeaponAttack: true })
                .ToList();

            if (unitsWithWeaponTargets.Count > 0)
            {
                _unitsWithTargets[player.Id] = unitsWithWeaponTargets;
            }
        }
    }

    private List<AttackQueueItem> BuildAttackQueue()
    {
        var queue = new List<AttackQueueItem>();
        foreach (var player in _playersInOrder)
        {
            if (!_unitsWithTargets.TryGetValue(player.Id, out var unitsWithTargets))
                continue;
            foreach (var unit in unitsWithTargets)
            {
                var weaponTargets = unit.DeclaredWeaponTargets ?? [];
                foreach (var weaponTarget in weaponTargets)
                {
                    var primaryAssignment = weaponTarget.Weapon.Assignments[0];
                    var weapon = unit.GetMountedComponentAtLocation<Weapon>(primaryAssignment.Location, primaryAssignment.FirstSlot);
                    var allUnits = Game.Players.SelectMany(p => p.Units);
                    var targetUnit = allUnits.FirstOrDefault(u => u.Id == weaponTarget.TargetId);
                    queue.Add(new AttackQueueItem(player, unit, weapon, targetUnit, weaponTarget));
                }
            }
        }
        return queue;
    }

    private void ResolveNextAttack()
    {
        if (Game.BattleMap == null)
        {
            throw new Exception("Battle map is null");
        }

        var attackQueue = BuildAttackQueue();

        foreach (var item in attackQueue)
        {
            var currentUnit = item.Attacker;
            var currentWeapon = item.Weapon;
            var targetUnit = item.TargetUnit;

            if (currentWeapon != null && targetUnit is { Position: not null } && currentUnit.Position != null)
            {
                var reversedLosResult = Game.BattleMap.GetLineOfSight(
                    targetUnit.Position.Coordinates,
                    currentUnit.Position.Coordinates,
                    targetUnit.Height,
                    currentUnit.Height);
                var attackerHasPartialCover = Game.RulesProvider.HasPartialCover(currentUnit, reversedLosResult);
                var canBeCovered = Game.RulesProvider.CanPartBeCovered(item.WeaponTargetData.Weapon.Assignments[0].Location);

                if (attackerHasPartialCover && canBeCovered)
                {
                    Game.Logger.LogInformation(
                        "Skipping leg-mounted weapon {WeaponName} attack from {AttackerName} to {TargetName} - attacker has partial cover",
                        currentWeapon.Name, currentUnit.Name, targetUnit.Name);
                }
                else
                {
                    var resolution = _weaponAttackResolver.ResolveAttack(currentUnit, targetUnit, currentWeapon, item.WeaponTargetData, Game.BattleMap);
                    FinalizeAttackResolution(item.Player, currentUnit, currentWeapon, targetUnit, resolution);
                }
            }
        }

        CalculateEndOfPhasePsrs();
        Game.TransitionToNextPhase(Name);
    }

    private void FinalizeAttackResolution(IPlayer player, IUnit attacker, Weapon weapon, IUnit target,
        AttackResolutionData resolution)
    {
        // Track destroyed parts before damage
        var destroyedPartsBefore = target.Parts.Values
            .Where(p => p.IsDestroyed).Select(p => p.Location).ToList();
        var wasDestroyedBefore = target.IsDestroyed;

        // Apply damage to the target
        if (resolution is { IsHit: true, HitLocationsData.HitLocations: not null })
        {
            target.ApplyDamage(resolution.HitLocationsData.HitLocations, resolution.AttackDirection);

            // Apply external heat if the weapon has ExternalHeat property
            if (weapon.ExternalHeat > 0 
                && resolution.HitLocationsData.HitLocations.Any(h => h.CoveringHexAbsorption == null))
            {
                target.AddExternalHeat(weapon.Name, weapon.ExternalHeat);
            }
        }

        // Check which parts are newly destroyed
        var destroyedPartsAfter = target.Parts.Values.Where(p => p.IsDestroyed).Select(p => p.Location).ToList();
        var newlyDestroyedParts = destroyedPartsAfter.Except(destroyedPartsBefore).ToList();

        // Check if the unit was destroyed by this attack
        var unitNewlyDestroyed = !wasDestroyedBefore && target.IsDestroyed;

        // Update the resolution data with destruction information
        resolution = resolution with
        {
            DestroyedParts = newlyDestroyedParts.Count != 0 ? newlyDestroyedParts : null,
            UnitDestroyed = unitNewlyDestroyed
        };

        // Create and publish a command to inform clients about the attack resolution
        var command = new WeaponAttackResolutionCommand
        {
            GameOriginId = Game.Id,
            PlayerId = player.Id,
            AttackerId = attacker.Id,
            WeaponData = weapon.ToData(),
            TargetId = target.Id,
            ResolutionData = resolution
        };

        attacker.FireWeapon(command.WeaponData);

        Game.CommandPublisher.PublishCommand(command);

        IEnumerable<ComponentHitData> allComponentHits = [];
        List<PartLocation> blownOffParts = [];

        // Calculate and send hull breach if target was submerged and took damage
        if (resolution.HitLocationsData?.HitLocations != null
            && resolution.HitLocationsData.HitLocations
                .Any(h => h.Damage.Any(d => d.ArmorDamage > 0 || d.StructureDamage > 0)))
        {
            var hullBreachCommand = Game.HullBreachCalculator
                .CalculateAndApplyHullBreach(target,
                    resolution.HitLocationsData.HitLocations
                        .SelectMany(h => h.Damage).ToList());

            if (hullBreachCommand != null)
            {
                hullBreachCommand.GameOriginId = Game.Id;
                Game.CommandPublisher.PublishCommand(hullBreachCommand);
            }
        }

        // Calculate and send critical hits if any location received structure damage
        if (resolution.HitLocationsData?.HitLocations != null
            && resolution.HitLocationsData.HitLocations
                .Any(h => h.Damage.Any(d => d.StructureDamage > 0)))
        {
            var criticalHitsCommand = Game.CriticalHitsCalculator
                .CalculateAndApplyCriticalHits(target,
                    (resolution.HitLocationsData?.HitLocations!) //nullability is checked above
                    .SelectMany(h => h.Damage).ToList());

            
            if (criticalHitsCommand != null)
            {
                criticalHitsCommand.GameOriginId = Game.Id;
                Game.CommandPublisher.PublishCommand(criticalHitsCommand);

                // Check for component hits that can cause a fall
                allComponentHits = criticalHitsCommand.CriticalHits.SelectMany(ch => ch.HitComponents ?? []);

                // Also track parts blown off by critical hits (e.g., leg/arm/head)
                blownOffParts = criticalHitsCommand.CriticalHits
                    .Where(ch => ch.IsBlownOff)
                    .Select(ch => ch.Location).ToList();
            }
        }

        // Process consciousness rolls for pilot damage from the attack and critical hits
        // This must happen AFTER both WeaponAttackResolutionCommand and CriticalHitsResolutionCommand are published
        // to maintain correct chronological order in the game log
        ProcessConsciousnessRollsForUnit(target);

        var allDestroyedParts = (resolution.DestroyedParts ?? [])
            .Concat(blownOffParts).Distinct();

        // Add component hits to accumulated damage data
        if (!_accumulatedDamageData.TryGetValue(target.Id, out var accumulatedDamage))
        {
            accumulatedDamage = new UnitPhaseAccumulatedDamage();
            _accumulatedDamageData[target.Id] = accumulatedDamage;
        }

        accumulatedDamage.AllComponentHits.AddRange(allComponentHits);
        foreach (var part in allDestroyedParts)
            accumulatedDamage.AllDestroyedParts.Add(part);
    }

    public override void HandleCommand(IGameCommand command)
    {
        // This phase doesn't process incoming commands, but we need to implement this method
    }

    public override PhaseNames Name => PhaseNames.WeaponAttackResolution;

    /// <summary>
    /// Calculates PSRs for all units that accumulated damage during the weapon attack resolution phase
    /// This is called at the end of the phase to comply with BattleTech rules
    /// </summary>
    private void CalculateEndOfPhasePsrs()
    {
        foreach (var (unitId, accumulatedDamage) in _accumulatedDamageData)
        {
            // Find the unit
            var unit = Game.Players
                .SelectMany(p => p.Units)
                .FirstOrDefault(u => u.Id == unitId);
                
            if (unit is not Mech targetMech) continue;
            
            // Calculate PSRs using the accumulated damage data from the entire phase
            var mechFallingCommands = Game.FallProcessor.ProcessPotentialFall(
                targetMech,
                Game,
                accumulatedDamage.AllComponentHits,
                accumulatedDamage.AllDestroyedParts);

            foreach (var fallingCommand in mechFallingCommands)
            {
                Game.OnMechFalling(fallingCommand);
                Game.CommandPublisher.PublishCommand(fallingCommand);
                if (fallingCommand.DamageData is null) break;

                var locationsWithDamagedStructure = fallingCommand.DamageData.HitLocations.HitLocations
                    .Where(h => h.Damage.Any(d => d.StructureDamage > 0))
                    .SelectMany(h => h.Damage)
                    .ToList();
                if (locationsWithDamagedStructure.Count != 0)
                {
                    var fallCriticalHitsCommand = Game.CriticalHitsCalculator
                        .CalculateAndApplyCriticalHits(targetMech, locationsWithDamagedStructure);
                    if (fallCriticalHitsCommand != null)
                    {
                        fallCriticalHitsCommand.GameOriginId = Game.Id;
                        Game.CommandPublisher.PublishCommand(fallCriticalHitsCommand);
                    }
                }
                // Process consciousness rolls for pilot damage accumulated during this phase
                ProcessConsciousnessRollsForUnit(targetMech);
                break;
            }
        }
        
        // Clear the accumulated damage data after processing
        _accumulatedDamageData.Clear();
    }

    /// <summary>
    /// Tracks accumulated damage data for a unit during the weapon attack resolution phase
    /// Used to calculate PSRs at the end of the phase instead of after each attack
    /// </summary>
    private record UnitPhaseAccumulatedDamage
    {
        public List<ComponentHitData> AllComponentHits { get; } = [];
        public List<PartLocation> AllDestroyedParts { get; } = [];
    }
}
