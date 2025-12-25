using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

public abstract class Weapon : Component
{
    protected Weapon(WeaponDefinition definition, ComponentData? componentData = null)
        : base(definition, componentData)
    {
    }

    // Cast the base definition to WeaponDefinition for weapon-specific properties
    private WeaponDefinition WeaponDefinition => (WeaponDefinition)_definition;

    public int Damage => WeaponDefinition.TotalDamage;
    public int Heat => WeaponDefinition.Heat;
    public int ExternalHeat => WeaponDefinition.ExternalHeat;
    public int MinimumRange => WeaponDefinition.MinimumRange;
    public int ShortRange => WeaponDefinition.ShortRange;
    public int MediumRange => WeaponDefinition.MediumRange;
    public int LongRange => WeaponDefinition.LongRange;
    public WeaponType Type => WeaponDefinition.Type;
    public int Clusters => WeaponDefinition.Clusters;
    public int ClusterSize => WeaponDefinition.ClusterSize;
    public int WeaponSize => WeaponDefinition.WeaponSize;

    public MakaMekComponent? AmmoType => WeaponDefinition.AmmoComponentType;
        
    /// <summary>
    /// Indicates whether this weapon requires ammunition to fire
    /// </summary>
    public bool RequiresAmmo => WeaponDefinition.RequiresAmmo;

    /// <summary>
    /// Gets the range bracket for a given distance
    /// </summary>
    public WeaponRange GetRangeBracket(int distance) => WeaponDefinition.GetRangeBracket(distance);
    
    /// <summary>
    /// Indicates whether this weapon is capable of making aimed shots
    /// </summary>
    public virtual bool IsAimShotCapable => true;
    
    public HexDirection? Facing => FirstMountPart?.Facing;

    public IReadOnlyList<FiringArc> GetFiringArcs()
    {

        if (FirstMountPart == null)
            return [];
        return FirstMountPart.GetFiringArcs(WeaponDefinition.MountingOptions);
    }
}
