using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public sealed class UpperLegActuator(ComponentData? componentData = null) : Component(Definition, componentData)
{
    public static readonly ActuatorDefinition Definition = new(
        "Upper Leg Actuator",
        MakaMekComponent.UpperLegActuator);

    public static readonly int[] DefaultMountSlots = [1];
}
