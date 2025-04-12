using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class Cockpit() : Component("Cockpit", [2])
{
    public override MakaMekComponent ComponentType => MakaMekComponent.Cockpit;
    public override bool IsRemovable => false;
}
