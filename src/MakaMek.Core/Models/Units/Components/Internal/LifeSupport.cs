using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class LifeSupport : Component
{
    // Life Support takes slots 1 and 6 in head
    private static readonly int[] LifeSupportSlots = [0, 5];

    public LifeSupport() : base("Life Support", LifeSupportSlots)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.LifeSupport;
    public override bool IsRemovable => false;
}
