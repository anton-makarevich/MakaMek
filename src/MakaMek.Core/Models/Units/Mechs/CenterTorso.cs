using Sanet.MakaMek.Core.Models.Units.Components.Internal;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class CenterTorso : Torso
{
    public CenterTorso(string name, int maxArmor, int maxRearArmor, int maxStructure) 
        : base(name, PartLocation.CenterTorso, maxArmor, maxRearArmor, maxStructure)
    {
        // Add default components
        TryAddComponent(new Gyro());
    }

    public override int ApplyDamage(int damage, HitDirection direction = HitDirection.Front)
    {
        var damageApplied = base.ApplyDamage(damage, direction);
        if (IsDestroyed)
        {
            Unit?.AddEvent(new UiEvent(UiEventType.UnitDestroyed, Unit.Name));
        }
        return damageApplied;
    }
}