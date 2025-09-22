using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public sealed class FootActuator(ComponentData? componentData = null) : Component(Definition, componentData)
{
    public static readonly ActuatorDefinition Definition = new(
        "Foot Actuator",
        MakaMekComponent.FootActuator);

    public static readonly int[] DefaultMountSlots = [3];
}
