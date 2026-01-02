using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public abstract class Torso : UnitPart
{
    public int MaxRearArmor { get; }
    public int CurrentRearArmor { get; private set; }

    public override bool IsPristine => base.IsPristine && CurrentRearArmor == MaxRearArmor;
    
    internal override bool CanBeBlownOff => false;
    
    private HexDirection? _facingOverride;

    protected Torso(string name, PartLocation location, int maxArmor, int maxRearArmor, int maxStructure) 
        : base(name, location, maxArmor, maxStructure, 12)
    {
        MaxRearArmor = maxRearArmor;
        CurrentRearArmor = maxRearArmor;
        ResetRotation(); // Initialize facing to match the unit's leg facing
    }
    
    public override HexDirection? Facing => _facingOverride ?? Unit?.Position?.Facing;

    public void Rotate(HexDirection newFacing)
    {
        _facingOverride = newFacing;
    }

    public void ResetRotation()
    {
        _facingOverride = null;
    }

    protected override int ReduceArmor(int damage, HitDirection direction)
    {
        if (direction != HitDirection.Rear) return base.ReduceArmor(damage, direction);
        // First, reduce rear armor
        var remainingDamage = damage;
        if (CurrentRearArmor <= 0) return damage;
        if (CurrentRearArmor >= remainingDamage)
        {
            CurrentRearArmor -= remainingDamage;
            Unit?.AddEvent(new UiEvent(UiEventType.ArmorDamage, Name, remainingDamage.ToString()));
            return 0;
        }
        remainingDamage -= CurrentRearArmor;
        Unit?.AddEvent(new UiEvent(UiEventType.ArmorDamage, Name, CurrentRearArmor.ToString()));
        CurrentRearArmor = 0;
        return remainingDamage;
    }

    /// <summary>
    /// Restores the torso state including rear armor from serialized data.
    /// Used by MechFactory when creating units from saved data.
    /// </summary>
    internal void RestoreTorsoState(int currentFrontArmor, int currentRearArmor, int currentStructure, bool isBlownOff)
    {
        RestoreState(currentFrontArmor, currentStructure, isBlownOff);
        CurrentRearArmor = currentRearArmor;
    }
    
    public override UnitPartStateData ToData()
    {
        return base.ToData() with { CurrentRearArmor = CurrentRearArmor };
    }

    public override IReadOnlyList<WeaponConfigurationOptions> GetWeaponsConfigurationOptions()
    {
        return this.GetAvailableTorsoRotationOptions();
    }
}