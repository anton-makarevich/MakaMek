using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public class HeatSink : Component
{
    public static readonly EquipmentDefinition Definition = new(
        "Heat Sink",
        MakaMekComponent.HeatSink,
        0, // No battle value
        1, // 1 slot
        1, // 1 health point
        true); // Removable

    public HeatSink(ComponentData? componentData = null) : base(Definition, componentData)
    {
        HeatDissipation = 1;
    }

    public HeatSink(int dissipation, string name, ComponentData? componentData = null)
        : base(new EquipmentDefinition(name, MakaMekComponent.HeatSink, 0, 1, 1, true), componentData)
    {
        HeatDissipation = dissipation;
    }

    public int HeatDissipation { get; }
}
