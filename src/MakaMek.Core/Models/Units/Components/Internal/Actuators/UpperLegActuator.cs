using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class UpperLegActuator : Component
{
    public static readonly ActuatorDefinition Definition = new(
        "Upper Leg",
        MakaMekComponent.UpperLegActuator);

    public UpperLegActuator(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }
}
