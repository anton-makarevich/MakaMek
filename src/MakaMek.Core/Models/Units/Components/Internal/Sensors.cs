using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class Sensors() : Component("Sensors", [1, 4], healthPoints:2)
{
    public override MakaMekComponent ComponentType => MakaMekComponent.Sensors;
    public override bool IsRemovable => false;
}
