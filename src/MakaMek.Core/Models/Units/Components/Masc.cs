using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public class Masc : Component
{
    
    public static readonly EquipmentDefinition Definition = new(
        "MASC",
        MakaMekComponent.Masc,
        0, // To be updated
        1, // 1 slot
        1, // 1 health point
        true); // Removable

    public Masc(ComponentData? componentData = null)
        : base(Definition, componentData)
    {
        Deactivate(); // MASC starts deactivated
    }
    
    public override void Hit()
    {
        base.Hit();
        Deactivate();
    }
}