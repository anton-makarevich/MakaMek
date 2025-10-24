using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// Wrapper class for unit selection result to satisfy reference type constraint
/// </summary>
public class UnitSelectionResult
{
    /// <summary>
    /// The selected unit, or null if cancelled
    /// </summary>
    public UnitData? SelectedUnit { get; init; }

    /// <summary>
    /// Creates a result with a selected unit
    /// </summary>
    public static UnitSelectionResult WithUnit(UnitData unit) => new() { SelectedUnit = unit };

    /// <summary>
    /// Creates a result indicating cancellation (no unit selected)
    /// </summary>
    public static UnitSelectionResult Cancelled() => new() { SelectedUnit = null };
}

