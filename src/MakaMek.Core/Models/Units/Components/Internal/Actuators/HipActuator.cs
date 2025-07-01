using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class HipActuator() : Component("Hip",[0])
{
    public override MakaMekComponent ComponentType => MakaMekComponent.Hip;
    public override bool IsRemovable => false;
}
