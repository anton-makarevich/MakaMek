using System.Collections.ObjectModel;
using System.Windows.Input;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// ViewModel for the AvailableUnitsTable control, handling unit filtering and selection
/// </summary>
public class AvailableUnitsTableViewModel : BindableBase
{
    private readonly ObservableCollection<UnitData> _availableUnits;
    private UnitData? _selectedUnit;
    private WeightClass _selectedWeightClassFilter = WeightClass.Light; // Default to first real class
    private bool _showAllClasses;

    public AvailableUnitsTableViewModel(
        ObservableCollection<UnitData> availableUnits,
        ICommand addUnitCommand,
        Func<bool> canAddUnit,
        ICommand closeTableCommand)
    {
        _availableUnits = availableUnits;
        AddUnitCommand = addUnitCommand;
        CanAddUnit = canAddUnit;
        CloseTableCommand = closeTableCommand;

        // Initialize with "All" filter selected
        _showAllClasses = true;
    }

    /// <summary>
    /// Gets the filtered list of available units based on the selected weight class filter
    /// </summary>
    public IEnumerable<UnitData> FilteredAvailableUnits
    {
        get
        {
            if (_showAllClasses)
                return _availableUnits;
            
            return _availableUnits.Where(u => GetWeightClass(u.Mass) == _selectedWeightClassFilter);
        }
    }

    /// <summary>
    /// Gets the list of weight class filter options (including "All")
    /// </summary>
    public List<string> WeightClassFilters { get; } =
    [
        "All",
        "Light",
        "Medium",
        "Heavy",
        "Assault"
    ];

    /// <summary>
    /// Gets or sets the selected weight class filter as a string
    /// </summary>
    public string SelectedWeightClassFilterString
    {
        get => _showAllClasses ? "All" : _selectedWeightClassFilter.ToString();
        set
        {
            if (value == "All")
            {
                _showAllClasses = true;
                _selectedWeightClassFilter = WeightClass.Light; // Default value when showing all
            }
            else if (Enum.TryParse<WeightClass>(value, out var weightClass) && weightClass != WeightClass.Unknown)
            {
                _showAllClasses = false;
                _selectedWeightClassFilter = weightClass;
            }
            
            NotifyPropertyChanged(nameof(SelectedWeightClassFilterString));
            NotifyPropertyChanged(nameof(FilteredAvailableUnits));
        }
    }

    /// <summary>
    /// Gets or sets the currently selected unit
    /// </summary>
    public UnitData? SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            SetProperty(ref _selectedUnit, value);
            NotifyPropertyChanged(nameof(CanAddUnitNow));
        }
    }

    /// <summary>
    /// Gets whether a unit can be added right now (unit is selected and player can add units)
    /// </summary>
    public bool CanAddUnitNow => _selectedUnit.HasValue && CanAddUnit();

    /// <summary>
    /// Command to add the selected unit
    /// </summary>
    public ICommand AddUnitCommand { get; }

    /// <summary>
    /// Command to close/hide the table
    /// </summary>
    public ICommand CloseTableCommand { get; }

    /// <summary>
    /// Function to check if units can be added (from PlayerViewModel)
    /// </summary>
    private Func<bool> CanAddUnit { get; }

    /// <summary>
    /// Calculates the weight class based on tonnage (matches Unit.cs logic)
    /// </summary>
    private static WeightClass GetWeightClass(int tonnage)
    {
        return tonnage switch
        {
            <= 35 => WeightClass.Light,
            <= 55 => WeightClass.Medium,
            <= 75 => WeightClass.Heavy,
            <= 100 => WeightClass.Assault,
            _ => WeightClass.Unknown
        };
    }

    /// <summary>
    /// Clears the selected unit (called after adding)
    /// </summary>
    public void ClearSelection()
    {
        SelectedUnit = null;
    }
}

