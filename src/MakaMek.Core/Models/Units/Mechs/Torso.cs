using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public abstract class Torso : UnitPart
{
    public int MaxRearArmor { get; }
    public int CurrentRearArmor { get; private set; }
    public HexDirection Facing { get; private set; }

    protected Torso(string name, PartLocation location, int maxArmor, int maxRearArmor, int maxStructure) 
        : base(name, location, maxArmor, maxStructure, 12)
    {
        MaxRearArmor = maxRearArmor;
        CurrentRearArmor = maxRearArmor;
        ResetRotation(); // Initialize facing to match unit's facing
    }

    public void Rotate(HexDirection newFacing)
    {
        Facing = newFacing;
    }

    public void ResetRotation()
    {
        if (Unit?.Position != null)
            Facing = Unit.Position.Facing;
    }

    protected override int ReduceArmor(int damage, HitDirection direction)
    {
        if (direction != HitDirection.Rear) return base.ReduceArmor(damage, direction);
        // First reduce rear armor
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

    internal override bool CanBeBlownOff => false;
}