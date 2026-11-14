using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Default projection from the unit domain model to evaluation snapshots.
/// </summary>
public sealed class UnitSnapshotFactory : IUnitSnapshotFactory
{
    public UnitSnapshot Create(IUnit unit)
    {
        var position = unit.Position;
        var movementPath = unit.MovementTaken
                           ?? (position == null ? null : MovementPath.CreateSingleSegmentPath(position));

        return Build(unit, position, movementPath, unit.GetAvailableWeapons(), includeConfigurations: false);
    }

    public UnitSnapshot CreateForAttacker(IUnit unit, MovementPath attackerPath)
    {
        // Attack evaluation historically enumerates every available weapon on the parts,
        // so keep that projection here instead of using the cached per-unit list.
        var weapons = unit.Parts.Values
            .SelectMany(p => p.GetComponents<Weapon>())
            .Where(w => w.IsAvailable)
            .ToList();

        return Build(unit, attackerPath.Destination, attackerPath, weapons, includeConfigurations: true);
    }

    private static UnitSnapshot Build(
        IUnit unit,
        HexPosition? position,
        MovementPath? movementPath,
        IReadOnlyList<Weapon> weapons,
        bool includeConfigurations)
    {
        return new UnitSnapshot
        {
            Id = unit.Id,
            Position = position,
            MovementPath = movementPath,
            Height = unit.Height,
            IsProne = unit.Status.HasFlag(UnitStatus.Prone),
            Gunnery = unit.Pilot?.Gunnery,
            Weapons = weapons.Select(w => CreateWeapon(unit, w)).ToList(),
            Configurations = includeConfigurations ? BuildConfigurations(unit, position) : [],
            ConfigurationTypesByPart = includeConfigurations
                ? BuildConfigurationTypes(unit, position)
                : new Dictionary<PartLocation, HashSet<WeaponConfigurationType>>()
        };
    }

    private static WeaponSnapshot CreateWeapon(IUnit unit, Weapon weapon)
    {
        var mountLocation = weapon.FirstMountPartLocation;
        return new WeaponSnapshot
        {
            Weapon = weapon,
            Damage = weapon.Damage,
            LongRange = weapon.Range?.LongRange,
            MountLocation = mountLocation,
            MountFacing = weapon.FirstMountPart?.Facing,
            FiringArcs = weapon.GetFiringArcs(),
            AttackModifiers = mountLocation == null ? [] : unit.GetAttackModifiers(mountLocation.Value)
        };
    }

    private static List<WeaponConfiguration> BuildConfigurations(IUnit unit, HexPosition? position)
    {
        if (position == null)
            return [];

        var torsoConfigs = unit.GetWeaponsConfigurationOptions(position)
            .Where(o => o.Type == WeaponConfigurationType.TorsoRotation)
            .SelectMany(o => o.AvailableDirections.Select(d => new WeaponConfiguration
            {
                Type = WeaponConfigurationType.TorsoRotation,
                Value = (int)d
            }))
            .ToList();

        // Prepend the "no rotation" option (current leg facing)
        torsoConfigs.Insert(0, new WeaponConfiguration
        {
            Type = WeaponConfigurationType.None,
            Value = (int)position.Facing
        });

        return torsoConfigs;
    }

    private static Dictionary<PartLocation, HashSet<WeaponConfigurationType>> BuildConfigurationTypes(
        IUnit unit,
        HexPosition? position)
    {
        var configurationTypes = new Dictionary<PartLocation, HashSet<WeaponConfigurationType>>();
        foreach (var part in unit.Parts.Values)
        {
            configurationTypes[part.Location] = part.GetWeaponsConfigurationOptions(position)
                .Select(o => o.Type)
                .ToHashSet();
        }

        return configurationTypes;
    }
}
