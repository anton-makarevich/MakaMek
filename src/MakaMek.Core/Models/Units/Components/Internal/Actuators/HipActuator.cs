using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class HipActuator : Component
{
    public static readonly ActuatorDefinition Definition = new(
        "Hip",
        MakaMekComponent.Hip);

    public HipActuator(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }
}
