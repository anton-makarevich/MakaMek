using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

/// <summary>
/// Represents the core definition of a weapon system, used by both Weapon and Ammo classes.
/// This allows sharing common properties and damage calculation between weapons and their ammunition.
/// </summary>
public class WeaponDefinition
{
    public WeaponDefinition(
        string name,
        int elementaryDamage,
        int heat,
        int minimumRange,
        int shortRange,
        int mediumRange,
        int longRange,
        WeaponType type,
        int battleValue,
        int clusters = 1,
        int clusterSize = 1,
        int size = 1,
        MakaMekComponent weaponComponentType = MakaMekComponent.MachineGun,
        MakaMekComponent? ammoComponentType = null)
    {
        Name = name;
        ElementaryDamage = elementaryDamage;
        Heat = heat;
        MinimumRange = minimumRange;
        ShortRange = shortRange;
        MediumRange = mediumRange;
        LongRange = longRange;
        Type = type;
        BattleValue = battleValue;
        Clusters = clusters;
        ClusterSize = clusterSize;
        Size = size;
        WeaponComponentType = weaponComponentType;
        AmmoComponentType = ammoComponentType;
    }

    public string Name { get; }
    public int ElementaryDamage { get; }
    public int Heat { get; }
    public int MinimumRange { get; }
    public int ShortRange { get; }
    public int MediumRange { get; }
    public int LongRange { get; }
    public WeaponType Type { get; }
    public int BattleValue { get; }
    public int Clusters { get; }
    public int ClusterSize { get; }

    public int Size { get; }
    public MakaMekComponent WeaponComponentType { get; }
    public MakaMekComponent? AmmoComponentType { get; }

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
