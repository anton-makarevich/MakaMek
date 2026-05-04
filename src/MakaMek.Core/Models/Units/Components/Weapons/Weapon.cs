using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

public abstract class Weapon : Component
{

    protected Weapon(WeaponDefinition definition, ComponentData? componentData = null)
        : base(GetDefinitionWithSpecificData(definition, componentData), componentData)
    {
        if (WeaponDefinition.MountingOptions == MountingOptions.Rear)
            Name = Name + " R";
    }

    /// <summary>
    /// Applies ComponentSpecificData to the base weapon definition
    /// </summary>
    private static WeaponDefinition GetDefinitionWithSpecificData(
        WeaponDefinition baseDefinition, 
        ComponentData? componentData)
    {
        if (componentData?.SpecificData is WeaponStateData weaponState)
        {
            return baseDefinition with { MountingOptions = weaponState.MountingOptions };
        }
        return baseDefinition;
    }

    // Cast the base definition to WeaponDefinition for weapon-specific properties
    internal WeaponDefinition WeaponDefinition => (WeaponDefinition)_definition;

    public int Damage => WeaponDefinition.TotalDamage;
    public int Heat => WeaponDefinition.Heat;
    public int ExternalHeat => WeaponDefinition.ExternalHeat;

    /// <summary>
    /// The standard range values for this weapon
    /// </summary>
    public WeaponRange Range => WeaponDefinition.Range;

    /// <summary>
    /// The underwater range values for this weapon (null if weapon cannot fire underwater)
    /// </summary>
    public WeaponRange? UnderwaterRange => WeaponDefinition.UnderwaterRange;

    /// <summary>
    /// Indicates whether this weapon can fire while submerged underwater
    /// </summary>
    public bool CanFireUnderwater => WeaponDefinition.CanFireUnderwater;

    /// <summary>
    /// Gets the appropriate range based on whether the attack is underwater
    /// </summary>
    public WeaponRange GetEffectiveRange(bool isUnderwater) =>
        isUnderwater && CanFireUnderwater && UnderwaterRange != null
            ? UnderwaterRange
            : Range;

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
    /// Indicates whether this weapon is capable of making aimed shots
    /// </summary>
    public virtual bool IsAimShotCapable => true;

    public IReadOnlyList<FiringArc> GetFiringArcs()
    {
        if (FirstMountPart == null)
            return [];
        return FirstMountPart.GetFiringArcs(WeaponDefinition.MountingOptions);
    }

    /// <summary>
    /// Override to persist weapon-specific state data for serialization
    /// </summary>
    protected override ComponentSpecificData? GetSpecificData()
    {
        // Only persist if mounting options differ from the standard definition
        return WeaponDefinition.MountingOptions != MountingOptions.Standard 
            ? new WeaponStateData(WeaponDefinition.MountingOptions) 
            : null;
    }
}
