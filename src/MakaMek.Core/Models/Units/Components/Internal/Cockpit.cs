using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class Cockpit() : Component("Cockpit")
{
    public override MakaMekComponent ComponentType => MakaMekComponent.Cockpit;
    public override bool IsRemovable => false;

    public override void Hit()
    {
        base.Hit();
        // Kill the pilot
        GetPrimaryMountLocation()?.Unit?.Pilot?.Kill();
    }
}
