using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class LowerLegActuator : Component
{
    public static readonly ActuatorDefinition Definition = new(
        "Lower Leg",
        MakaMekComponent.LowerLegActuator);

    public LowerLegActuator(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.LowerLegActuator;
}
