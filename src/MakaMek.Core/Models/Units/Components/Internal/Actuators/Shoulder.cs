using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class Shoulder() : Component("Shoulder", [0])
{
    public override MakaMekComponent ComponentType => MakaMekComponent.Shoulder;
    public override bool IsRemovable => false;
}
