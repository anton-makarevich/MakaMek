using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// ViewModel for a unit in the end game summary
/// </summary>
public class EndGameUnitViewModel
{
    private readonly IUnit _unit;

    public EndGameUnitViewModel(IUnit unit, ILocalizationService? localizationService = null)
    {
        _unit = unit;
        UnitData = unit.ToData();
        PilotData = unit.Pilot?.ToData();
        CardViewModel = new UnitViewModel(UnitData, PilotData, localizationService);
    }

    public UnitData UnitData { get; }

    /// <summary>
    /// Gets the snapshot of the pilot assigned to the unit, if any
    /// </summary>
    public PilotData? PilotData { get; }

    /// <summary>
    /// Gets the card view model shared with the unit card control
    /// </summary>
    public UnitViewModel CardViewModel { get; }

    /// <summary>
    /// Gets the unit's name
    /// </summary>
    public string Name => _unit.Name;

    /// <summary>
    /// Gets the unit's chassis
    /// </summary>
    public string Chassis => _unit.Chassis;

    /// <summary>
    /// Gets the unit's model
    /// </summary>
    public string Model => _unit.Model;

    /// <summary>
    /// Gets the unit's tonnage
    /// </summary>
    public int Tonnage => _unit.Tonnage;

    /// <summary>
    /// Gets the unit's weight class
    /// </summary>
    public string WeightClass => _unit.Class.ToString();

    /// <summary>
    /// Gets the unit's status
    /// </summary>
    public string Status => _unit.Status.ToString();

    /// <summary>
    /// Gets whether the unit is destroyed
    /// </summary>
    public bool IsDestroyed => _unit.IsDestroyed;

    /// <summary>
    /// Gets whether the unit is alive
    /// </summary>
    public bool IsAlive => !_unit.IsDestroyed;

    /// <summary>
    /// Gets the pilot's name, if any
    /// </summary>
    public string? PilotName => _unit.Pilot?.Name;

    /// <summary>
    /// Gets whether the pilot is dead
    /// </summary>
    public bool IsPilotDead => _unit.Pilot?.IsDead ?? false;

    /// <summary>
    /// Gets whether the pilot is unconscious
    /// </summary>
    public bool IsPilotUnconscious => _unit.Pilot?.IsConscious == false;
}

