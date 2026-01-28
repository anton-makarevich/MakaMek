using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

public sealed class Ammo : Component
{
    private int _remainingShots;
    private readonly decimal _massRoundsMultiplier = 1m;

    public Ammo(WeaponDefinition definition, ComponentData? componentData = null)
        : base(CreateAmmoDefinition(definition, componentData), componentData)
    {
        Definition = definition;

        // Set remaining shots from component data or use the initial value
        var maxRounds = definition.FullAmmoRounds;
        if (componentData?.SpecificData is AmmoStateData ammoState)
        {
            _massRoundsMultiplier = ammoState.MassRoundsMultiplier;
            if (ammoState.RemainingShots != null)
            {
                _remainingShots = Math.Max(ammoState.RemainingShots.Value, 0);
            }
        }
        else
        {
            _remainingShots = _massRoundsMultiplier == 1m
                ? maxRounds 
                :Math.Max((int)(maxRounds * _massRoundsMultiplier), 0);
        }
    }

    public static ComponentDefinition CreateAmmoDefinition(WeaponDefinition weaponDefinition, ComponentData? componentData = null)
    {
        if (!weaponDefinition.RequiresAmmo)
        {
            throw new ArgumentException($"Cannot create ammo for weapon that doesn't require it: {weaponDefinition.Name}");
        }
        var massRoundsMultiplier = componentData?.SpecificData is AmmoStateData ammoState
            ? ammoState.MassRoundsMultiplier
            : 1m;
        return new EquipmentDefinition(
            $"{weaponDefinition.Name} Ammo",
            weaponDefinition.AmmoComponentType ?? throw new InvalidOperationException("Ammo component type not defined"),
            Mass: 1m * massRoundsMultiplier); 
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
        return new AmmoStateData(RemainingShots, _massRoundsMultiplier);
    }

    public override bool IsAvailable => base.IsAvailable && RemainingShots > 0;
}
