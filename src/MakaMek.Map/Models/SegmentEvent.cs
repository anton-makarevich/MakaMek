using Sanet.MakaMek.Map.Models.MovementCosts;

namespace Sanet.MakaMek.Map.Models;

public record struct SegmentEvent(SegmentEventType Type, IReadOnlyList<MovementCost> Costs)
{
    public int Cost => Costs.Sum(c => c.Value);
}
