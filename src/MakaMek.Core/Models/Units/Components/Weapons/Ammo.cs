using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

public class Ammo : Component
{
    private int _remainingShots;

    public Ammo(WeaponDefinition definition, int initialShots) : base($"{definition.Name} Ammo", [])
    {
        if (!definition.RequiresAmmo)
        {
            throw new ArgumentException($"Cannot create ammo for weapon that doesn't require it: {definition.Name}");
        }
        
        Definition = definition;
        _remainingShots = initialShots;
    }

    public WeaponDefinition Definition { get; }
    
    public int RemainingShots => _remainingShots;

    public bool UseShot()
    {
        if (_remainingShots <= 0)
            return false;

        _remainingShots--;
        return true;
    }

    public override MakaMekComponent ComponentType => Definition.AmmoComponentType ?? throw new InvalidOperationException("Ammo component type not defined");

    public override void Hit()
    {
        base.Hit();
        HasExploded = true;
        _remainingShots = 0;
    }

    // Explosion-related functionality
    public override bool CanExplode => true;

    public override int GetExplosionDamage()
    {
        if (HasExploded || RemainingShots <= 0)
            return 0;
            
        // Calculate explosion damage based on ammo type and remaining shots
        // Each shot does the weapon's total damage
        return Definition.TotalDamage * RemainingShots;
    }
}
