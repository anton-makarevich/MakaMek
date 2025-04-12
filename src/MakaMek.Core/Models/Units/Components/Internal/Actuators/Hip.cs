using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class Hip : Component
{
    private static readonly int[] HipSlots = { 0 };
    public Hip() : base("Hip", HipSlots)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.Hip;
    public override bool IsRemovable => false;
}
