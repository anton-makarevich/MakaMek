using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class LifeSupport() : Component("Life Support")
{
    public override MakaMekComponent ComponentType => MakaMekComponent.LifeSupport;
    public override bool IsRemovable => false;
}
