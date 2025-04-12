using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class Sensors() : Component("Sensors", [1, 4])
{
    public override MakaMekComponent ComponentType => MakaMekComponent.Sensors;
    public override bool IsRemovable => false;
}
