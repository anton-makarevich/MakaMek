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
        IList<UnitData> availableUnits,
        ICommand addUnitCommand)
    {
        _availableUnits = new ObservableCollection<UnitData>(availableUnits);
        AddUnitCommand = addUnitCommand;

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
            
            NotifyPropertyChanged();
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
            NotifyPropertyChanged(nameof(CanAddUnit));
        }
    }

    /// <summary>
    /// Gets whether a unit can be added right now (unit is selected and player can add units)
    /// </summary>
    public bool CanAddUnit => _selectedUnit.HasValue;

    /// <summary>
    /// Command to add the selected unit
    /// </summary>
    public ICommand AddUnitCommand { get; }

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
}

