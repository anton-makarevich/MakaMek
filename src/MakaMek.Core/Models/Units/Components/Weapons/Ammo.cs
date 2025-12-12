using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

public sealed class Ammo : Component
{
    private int _remainingShots;

    public Ammo(WeaponDefinition definition, ComponentData? componentData = null)
        : base(CreateAmmoDefinition(definition), componentData)
    {
        Definition = definition;

        // Set remaining shots from component data or use the initial value
        var maxRounds = definition.FullAmmoRounds;
        if (componentData?.SpecificData is AmmoStateData ammoState)
        {
            _remainingShots = Math.Max(ammoState.RemainingShots, 0);
        }
        else
        {
            _remainingShots = Math.Max(maxRounds, 0);
        }
    }

    public static ComponentDefinition CreateAmmoDefinition(WeaponDefinition weaponDefinition)
    {
        if (!weaponDefinition.RequiresAmmo)
        {
            throw new ArgumentException($"Cannot create ammo for weapon that doesn't require it: {weaponDefinition.Name}");
        }
        return new EquipmentDefinition(
            $"{weaponDefinition.Name} Ammo",
            weaponDefinition.AmmoComponentType ?? throw new InvalidOperationException("Ammo component type not defined")); 
    }

    public WeaponDefinition Definition { get; }
    
    public int RemainingShots => _remainingShots;

    public bool UseShot()
    {
        if (_remainingShots <= 0 || !IsActive || HasExploded)
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

    protected override ComponentSpecificData GetSpecificData()
    {
        return new AmmoStateData(RemainingShots);
    }

    public override bool IsAvailable => base.IsAvailable && RemainingShots > 0;
}
