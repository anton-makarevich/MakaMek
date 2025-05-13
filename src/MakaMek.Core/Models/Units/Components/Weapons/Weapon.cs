using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

public abstract class Weapon : Component
{
    private readonly WeaponDefinition _definition;

    protected Weapon(WeaponDefinition definition) 
        : base(definition.Name, [], definition.Size)
    {
        _definition = definition;
        BattleValue = definition.BattleValue;
    }

    public int Damage => _definition.TotalDamage;
    public int Heat => _definition.Heat;
    public int MinimumRange => _definition.MinimumRange;
    public int ShortRange => _definition.ShortRange;
    public int MediumRange => _definition.MediumRange;
    public int LongRange => _definition.LongRange;
    public WeaponType Type => _definition.Type;
    public int Clusters => _definition.Clusters;
    public int ClusterSize => _definition.ClusterSize;
    public int WeaponSize => _definition.WeaponSize;
    
    public MakaMekComponent? AmmoType => _definition.AmmoComponentType; 
        
    /// <summary>
    /// Indicates whether this weapon requires ammunition to fire
    /// </summary>
    public bool RequiresAmmo => _definition.RequiresAmmo;
    
    /// <summary>
    /// The target unit for this weapon in the current attack declaration
    /// </summary>
    public Unit? Target { get; set; }

    /// <summary>
    /// Gets the range bracket for a given distance
    /// </summary>
    public WeaponRange GetRangeBracket(int distance) => _definition.GetRangeBracket(distance);
    
    public override MakaMekComponent ComponentType => _definition.WeaponComponentType;
}
