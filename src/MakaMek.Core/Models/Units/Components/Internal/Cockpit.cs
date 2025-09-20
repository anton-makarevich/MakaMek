using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class Cockpit : Component
{
    public static readonly InternalDefinition Definition = new(
        "Cockpit",
        1, // 1 health point
        MakaMekComponent.Cockpit);

    public Cockpit(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }

    public override void Hit()
    {
        base.Hit();
        // Kill the pilot
        GetPrimaryMountLocation()?.Unit?.Pilot?.Kill();
    }
}
