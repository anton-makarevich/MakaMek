using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public record UnitPartViewModel
{
    public PartLocation Location { get; init; }
    public bool IsDestroyed { get; init; }
    public bool IsSelectable { get; init; }
    public double HitProbability { get; init; }
    public string HitProbabilityText { get; init; } = string.Empty;

    // Armor and Structure properties for progress bars
    public int CurrentArmor { get; init; }
    public int MaxArmor { get; init; }
    public int CurrentStructure { get; init; }
    public int MaxStructure { get; init; }

    public string Name { get; init; } = string.Empty;
}