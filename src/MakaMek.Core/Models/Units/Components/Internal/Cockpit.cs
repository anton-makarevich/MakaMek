using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public sealed class Cockpit(ComponentData? componentData = null) : Component(Definition, componentData)
{
    public static readonly InternalDefinition Definition = new(
        "Cockpit",
        1, // 1 health point
        MakaMekComponent.Cockpit);

    public static readonly int[] DefaultMountSlots = [2];

    public override void Hit()
    {
        var wasDestroyed = IsDestroyed;
        base.Hit();
        if (!wasDestroyed && IsDestroyed)
        {
            GetPrimaryMountLocation()?.Unit?.Pilot?.Kill();
        }
    }
}
