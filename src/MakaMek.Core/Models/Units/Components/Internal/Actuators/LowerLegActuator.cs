using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public sealed class LowerLegActuator(ComponentData? componentData = null) : Component(Definition, componentData)
{
    public static readonly ActuatorDefinition Definition = new(
        "Lower Leg Actuator",
        MakaMekComponent.LowerLegActuator);

    public static readonly int[] DefaultMountSlots = [2];
}
