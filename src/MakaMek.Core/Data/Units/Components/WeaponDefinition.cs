using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Data.Units.Components;

/// <summary>
/// Represents the core definition of a weapon system, used by both Weapon and Ammo classes.
/// This allows sharing common properties and damage calculation between weapons and their ammunition.
/// </summary>
public record WeaponDefinition(
    string Name,
    int ElementaryDamage,
    int Heat,
    WeaponRange Range,
    WeaponType Type,
    int BattleValue,
    WeaponRange? UnderwaterRange = null,
    int Clusters = 1,
    int ClusterSize = 1,
    int Size = 1,
    int FullAmmoRounds = 1,
    MakaMekComponent WeaponComponentType = MakaMekComponent.MachineGun, // TODO: why is that machine gun???
    MakaMekComponent? AmmoComponentType = null,
    int ExternalHeat = 0,
    MountingOptions MountingOptions = MountingOptions.Standard)
    : ComponentDefinition(Name, Size, 1, BattleValue, true, WeaponComponentType, 0m)
{
    /// <summary>
    /// Indicates whether this weapon requires ammunition to fire
    /// </summary>
    public bool RequiresAmmo => AmmoComponentType.HasValue;

    /// <summary>
    /// The total damage calculated as ElementaryDamage * Clusters * ClusterSize
    /// </summary>
    public int TotalDamage => ElementaryDamage * Clusters * ClusterSize;

    /// <summary>
    /// The total number of projectiles/missiles in a single shot
    /// </summary>
    public int WeaponSize => Clusters * ClusterSize;

    /// <summary>
    /// Indicates whether this weapon can fire underwater
    /// </summary>
    public bool CanFireUnderwater => UnderwaterRange != null;

    /// <summary>
    /// Gets the range bracket for a given distance using the standard range table
    /// </summary>
    public RangeBracket GetRangeBracket(int distance)
    {
        return GetRangeBracket(distance, Range);
    }

    /// <summary>
    /// Gets the range bracket for a given distance using the underwater range table.
    /// Returns OutOfRange if the weapon has no underwater range.
    /// </summary>
    public RangeBracket GetUnderwaterRangeBracket(int distance)
    {
        if (UnderwaterRange == null)
            return RangeBracket.OutOfRange;
        return GetRangeBracket(distance, UnderwaterRange);
    }

    private static RangeBracket GetRangeBracket(int distance, WeaponRange range)
    {
        if (distance <= 0)
            return RangeBracket.OutOfRange;

        if (distance <= range.MinimumRange)
            return RangeBracket.Minimum;

        if (distance <= range.ShortRange)
            return RangeBracket.Short;

        if (distance <= range.MediumRange)
            return RangeBracket.Medium;

        if (distance <= range.LongRange)
            return RangeBracket.Long;

        return RangeBracket.OutOfRange;
    }
}
