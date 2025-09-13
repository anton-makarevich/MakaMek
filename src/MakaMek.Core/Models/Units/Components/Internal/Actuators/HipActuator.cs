using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class HipActuator() : Component("Hip")
{
    public override MakaMekComponent ComponentType => MakaMekComponent.Hip;
    public override bool IsRemovable => false;
}
