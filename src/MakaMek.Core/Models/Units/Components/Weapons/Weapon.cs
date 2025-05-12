using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

public abstract class Weapon : Component
{
    protected Weapon(WeaponDefinition definition, int size = 1) 
        : base(definition.Name, [], size)
    {
        Definition = definition;
        BattleValue = definition.BattleValue;
    }

    public WeaponDefinition Definition { get; }
    
    public int Damage => Definition.TotalDamage;
    public int Heat => Definition.Heat;
    public int MinimumRange => Definition.MinimumRange;
    public int ShortRange => Definition.ShortRange;
    public int MediumRange => Definition.MediumRange;
    public int LongRange => Definition.LongRange;
    public WeaponType Type => Definition.Type;
    public int Clusters => Definition.Clusters;
    public int ClusterSize => Definition.ClusterSize;
    public int WeaponSize => Definition.WeaponSize;
    
    /// <summary>
    /// Indicates whether this weapon requires ammunition to fire
    /// </summary>
    public bool RequiresAmmo => Definition.RequiresAmmo;
    
    /// <summary>
    /// The target unit for this weapon in the current attack declaration
    /// </summary>
    public Unit? Target { get; set; }

    /// <summary>
    /// Gets the range bracket for a given distance
    /// </summary>
    public WeaponRange GetRangeBracket(int distance) => Definition.GetRangeBracket(distance);
    
    public override MakaMekComponent ComponentType => Definition.WeaponComponentType;
}
