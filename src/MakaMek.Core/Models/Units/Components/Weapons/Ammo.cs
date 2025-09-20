using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

public class Ammo : Component
{
    private int _remainingShots;

    public Ammo(WeaponDefinition definition, int initialShots, ComponentData? componentData = null)
        : base(CreateAmmoDefinition(definition), componentData)
    {
        if (!definition.RequiresAmmo)
        {
            throw new ArgumentException($"Cannot create ammo for weapon that doesn't require it: {definition.Name}");
        }

        Definition = definition;

        // Set remaining shots from component data or use initial value
        if (componentData?.SpecificData is AmmoStateData ammoState)
        {
            _remainingShots = ammoState.RemainingShots;
        }
        else
        {
            _remainingShots = initialShots;
        }
    }

    private static ComponentDefinition CreateAmmoDefinition(WeaponDefinition weaponDefinition)
    {
        return new EquipmentDefinition(
            $"{weaponDefinition.Name} Ammo",
            weaponDefinition.AmmoComponentType ?? throw new InvalidOperationException("Ammo component type not defined"),
            0, // Ammo has no battle value
            1, // Ammo takes 1 slot
            1, // Ammo has 1 health point
            true); // Ammo is removable
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



    public override void Hit()
    {
        base.Hit();
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

    protected override ComponentSpecificData? GetSpecificData()
    {
        return new AmmoStateData(RemainingShots);
    }
}
