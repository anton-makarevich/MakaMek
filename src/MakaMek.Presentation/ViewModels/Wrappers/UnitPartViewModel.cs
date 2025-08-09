using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class UnitPartViewModel
{
    public PartLocation Location { get; init; }
    public bool IsDestroyed { get; init; }
    public bool IsSelectable { get; init; }
    public double HitProbability { get; init; }
    public string HitProbabilityText { get; init; } = string.Empty;
}