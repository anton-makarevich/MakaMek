using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Represents the core definition of a weapon system, used by both Weapon and Ammo classes.
/// This allows sharing common properties and damage calculation between weapons and their ammunition.
/// </summary>
public record WeaponDefinition(
    string Name,
    int ElementaryDamage,
    int Heat,
    int MinimumRange,
    int ShortRange,
    int MediumRange,
    int LongRange,
    WeaponType Type,
    int BattleValue,
    int Clusters = 1,
    int ClusterSize = 1,
    int Size = 1,
    int FullAmmoRounds = 1,
    MakaMekComponent WeaponComponentType = MakaMekComponent.MachineGun,
    MakaMekComponent? AmmoComponentType = null)
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
    /// Gets the range bracket for a given distance
    /// </summary>
    public WeaponRange GetRangeBracket(int distance)
    {
        if (distance <= 0)
            return WeaponRange.OutOfRange;
        
        if (distance <= MinimumRange)
            return WeaponRange.Minimum;
            
        if (distance <= ShortRange)
            return WeaponRange.Short;
            
        if (distance <= MediumRange)
            return WeaponRange.Medium;
            
        if (distance <= LongRange)
            return WeaponRange.Long;
            
        return WeaponRange.OutOfRange;
    }
}
