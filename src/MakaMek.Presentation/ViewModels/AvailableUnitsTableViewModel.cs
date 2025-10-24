using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// ViewModel for the AvailableUnitsTable view, handling unit filtering, sorting, and selection
/// </summary>
public class AvailableUnitsTableViewModel : BaseViewModel, IResultProvider<UnitSelectionResult>
{
    private readonly ObservableCollection<UnitData> _availableUnits;
    private readonly TaskCompletionSource<UnitSelectionResult> _resultTaskCompletionSource = new();
    private UnitData? _selectedUnit;
    private WeightClass? _selectedWeightClassFilter; // Default is `all` so no filtering
    private bool _showAllClasses;
    private SortColumn _currentSortColumn = SortColumn.Name;
    private bool _isSortAscending = true;
    private const string FilterAllKey = "All";

    private enum SortColumn
    {
        Name,
        Tonnage
    }

    public AvailableUnitsTableViewModel(IList<UnitData> availableUnits)
    {
        _availableUnits = new ObservableCollection<UnitData>(availableUnits);

        // Initialize with "All" filter selected
        _showAllClasses = true;

        // Initialize commands
        SortByNameCommand = new AsyncCommand(SortByName);
        SortByTonnageCommand = new AsyncCommand(SortByTonnage);
        AddUnitCommand = new AsyncCommand(AddUnit, _ => CanAddUnit);
        CancelCommand = new AsyncCommand(Cancel);
    }

    /// <summary>
    /// Gets the filtered and sorted list of available units based on the selected weight class filter and sort settings
    /// </summary>
    public IEnumerable<UnitData> FilteredAvailableUnits
    {
        get
        {
            var filtered = _showAllClasses
                ? _availableUnits
                : _availableUnits.Where(u => u.Mass.ToWeightClass() == _selectedWeightClassFilter);

            return ApplySorting(filtered);
        }
    }

    /// <summary>
    /// Gets the list of weight class filter options (including "All")
    /// </summary>
    public List<string> WeightClassFilters { get; } = PrepareFilters();

    private static List<string> PrepareFilters()
    {
        var filters = Enum.GetNames<WeightClass>()
            .Where(c => c != nameof(WeightClass.Unknown))
            .ToList();
        
        filters.Insert(0, FilterAllKey);
        return filters;
    }

    /// <summary>
    /// Gets or sets the selected weight class filter as a string
    /// </summary>
    public string SelectedWeightClassFilterString
    {
        get => _showAllClasses ? FilterAllKey : _selectedWeightClassFilter?.ToString()??FilterAllKey;
        set
        {
            if (value == FilterAllKey)
            {
                _showAllClasses = true;
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
            (AddUnitCommand as AsyncCommand)?.RaiseCanExecuteChanged();
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
    /// Command to cancel unit selection
    /// </summary>
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Command to sort by Name column
    /// </summary>
    public ICommand SortByNameCommand { get; }

    /// <summary>
    /// Command to sort by Tonnage column
    /// </summary>
    public ICommand SortByTonnageCommand { get; }

    /// <summary>
    /// Gets the sort indicator for the Name column (↓ for ascending, ↑ for descending, empty if not sorted)
    /// </summary>
    public string NameSortIndicator => _currentSortColumn == SortColumn.Name
        ? (_isSortAscending ? "↓" : "↑")
        : string.Empty;

    /// <summary>
    /// Gets the sort indicator for the Tonnage column (↓ for ascending, ↑ for descending, empty if not sorted)
    /// </summary>
    public string TonnageSortIndicator => _currentSortColumn == SortColumn.Tonnage
        ? (_isSortAscending ? "↓" : "↑")
        : string.Empty;

    private Task SortByName()
    {
        if (_currentSortColumn == SortColumn.Name)
        {
            // Toggle sort order if already sorting by Name
            _isSortAscending = !_isSortAscending;
        }
        else
        {
            // Switch to Name column with ascending order
            _currentSortColumn = SortColumn.Name;
            _isSortAscending = true;
        }

        RefreshSorting();
        return Task.CompletedTask;
    }

    private Task SortByTonnage()
    {
        if (_currentSortColumn == SortColumn.Tonnage)
        {
            // Toggle sort order if already sorting by Tonnage
            _isSortAscending = !_isSortAscending;
        }
        else
        {
            // Switch to Tonnage column with ascending order
            _currentSortColumn = SortColumn.Tonnage;
            _isSortAscending = true;
        }

        RefreshSorting();
        return Task.CompletedTask;
    }

    private void RefreshSorting()
    {
        NotifyPropertyChanged(nameof(FilteredAvailableUnits));
        NotifyPropertyChanged(nameof(NameSortIndicator));
        NotifyPropertyChanged(nameof(TonnageSortIndicator));
    }

    private IEnumerable<UnitData> ApplySorting(IEnumerable<UnitData> units)
    {
        return _currentSortColumn switch
        {
            SortColumn.Name => _isSortAscending
                ? units.OrderBy(u => u.Chassis)
                    .ThenBy(u => u.Model)
                : units.OrderByDescending(u => u.Chassis)
                    .ThenByDescending(u => u.Model),
            SortColumn.Tonnage => _isSortAscending
                ? units.OrderBy(u => u.Mass)
                    .ThenBy(u => u.Chassis)
                : units.OrderByDescending(u => u.Mass)
                    .ThenByDescending(u => u.Chassis),
            _ => units
        };
    }

    private Task AddUnit()
    {
        if (!CanAddUnit) return Task.CompletedTask;

        // Complete the task with the selected unit
        _resultTaskCompletionSource.TrySetResult(UnitSelectionResult.WithUnit(_selectedUnit!.Value));
        return Task.CompletedTask;
    }

    private Task Cancel()
    {
        // Complete the task with cancelled result
        _resultTaskCompletionSource.TrySetResult(UnitSelectionResult.Cancelled());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a task that completes when a unit is selected or the dialog is cancelled.
    /// Note: This method returns the same task instance on every call. Once the task completes,
    /// all subsequent awaits will receive the same result. Create a new ViewModel instance for each dialog invocation.
    /// </summary>
    public Task<UnitSelectionResult> GetResultAsync()
    {
        return _resultTaskCompletionSource.Task;
    }
}

