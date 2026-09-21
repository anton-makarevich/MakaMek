using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Utils;
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
    private readonly IMechFactory _mechFactory;
    private UnitData? _selectedUnit;
    private WeightClass? _selectedWeightClassFilter; // Default is `all` so no filtering
    private bool _showAllClasses;
    private SortColumn _currentSortColumn = SortColumn.Name;
    private bool _isSortAscending = true;
    private string _searchText = string.Empty;
    private UnitSelectionPreviewViewModel? _selectedUnitPreview;
    private readonly int _usedBattleValue;
    private int _battleValueLimit;
    private const string FilterAllKey = "All";

    private enum SortColumn
    {
        Name,
        Tonnage
    }

    public AvailableUnitsTableViewModel(IList<UnitData> availableUnits, IMechFactory mechFactory,
        int battleValueLimit = 0, IEnumerable<UnitData>? selectedUnits = null)
    {
        _availableUnits = new ObservableCollection<UnitData>(availableUnits);
        _mechFactory = mechFactory;
        _battleValueLimit = Math.Max(0, battleValueLimit);
        _usedBattleValue = _battleValueLimit > 0
            ? selectedUnits?.Sum(GetBattleValue) ?? 0
            : 0;

        // Initialize with "All" filter selected
        _showAllClasses = true;

        // Initialize commands
        SortByNameCommand = new AsyncCommand(SortByName);
        SortByTonnageCommand = new AsyncCommand(SortByTonnage);
        AddUnitCommand = new AsyncCommand(AddUnit, _ => CanAddUnit);
        CancelCommand = new AsyncCommand(Cancel);
        ShowUnitInfoCommand = new AsyncCommand(ShowUnitInfo, _ => CanShowUnitInfo);
        ClearSearchCommand = new AsyncCommand(ClearSearch, _ => HasSearchText);
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

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var search = _searchText.Trim();
                filtered = filtered.Where(u => u.Chassis.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || u.Model.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (u.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                    || GetEra(u).Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (_battleValueLimit > 0)
                filtered = filtered.Where(u => GetBattleValue(u) <= RemainingBattleValue);

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
            _selectedUnitPreview = value is { } selected
                ? new UnitSelectionPreviewViewModel(selected, _mechFactory.Create(selected))
                : null;
            NotifyPropertyChanged(nameof(CanAddUnit));
            NotifyPropertyChanged(nameof(CanShowUnitInfo));
            NotifyPropertyChanged(nameof(SelectedUnitPreview));
            NotifyPropertyChanged(nameof(HasSelection));
            NotifyPropertyChanged(nameof(HasNoSelection));
            (AddUnitCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ShowUnitInfoCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Gets or sets the case-insensitive chassis/model search text.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
            NotifyPropertyChanged(nameof(FilteredAvailableUnits));
            NotifyPropertyChanged(nameof(FilteredUnitCount));
            NotifyPropertyChanged(nameof(HasNoResults));
            NotifyPropertyChanged(nameof(HasSearchText));
            (ClearSearchCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Gets the number of units currently visible after filtering and search.</summary>
    public int FilteredUnitCount => FilteredAvailableUnits.Count();

    /// <summary>Gets whether the current filters leave no selectable units.</summary>
    public bool HasNoResults => FilteredUnitCount == 0;

    /// <summary>Gets or sets the per-player BV cap; zero disables the cap.</summary>
    public int BattleValueLimit
    {
        get => _battleValueLimit;
        set
        {
            _battleValueLimit = Math.Max(0, value);
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(RemainingBattleValue));
            NotifyPropertyChanged(nameof(IsBattleValueLimitEnabled));
            NotifyPropertyChanged(nameof(CanAddUnit));
            NotifyPropertyChanged(nameof(FilteredAvailableUnits));
            NotifyPropertyChanged(nameof(FilteredUnitCount));
            NotifyPropertyChanged(nameof(HasNoResults));
        }
    }

    /// <summary>Gets the BV still available for the current player's force.</summary>
    public int RemainingBattleValue => _battleValueLimit > 0
        ? Math.Max(0, _battleValueLimit - _usedBattleValue)
        : 0;

    /// <summary>Gets whether the picker is applying a finite BV budget.</summary>
    public bool IsBattleValueLimitEnabled => _battleValueLimit > 0;

    /// <summary>Gets whether the preview pane has a selected mech to display.</summary>
    public bool HasSelection => _selectedUnit.HasValue;

    /// <summary>Gets whether the preview pane should show its selection prompt.</summary>
    public bool HasNoSelection => !HasSelection;

    /// <summary>Gets whether the search box currently contains text.</summary>
    public bool HasSearchText => !string.IsNullOrWhiteSpace(_searchText);

    /// <summary>
    /// Gets a rules-backed preview for the selected unit, or null before selection.
    /// </summary>
    public UnitSelectionPreviewViewModel? SelectedUnitPreview => _selectedUnitPreview;

    /// <summary>
    /// Gets whether a unit can be added right now (unit is selected and player can add units)
    /// </summary>
    public bool CanAddUnit => _selectedUnit.HasValue
        && (!IsBattleValueLimitEnabled || GetBattleValue(_selectedUnit.Value) <= RemainingBattleValue);

    /// <summary>
    /// Gets whether unit info can be shown (unit is selected)
    /// </summary>
    public bool CanShowUnitInfo => _selectedUnit.HasValue;

    /// <summary>
    /// Command to add the selected unit
    /// </summary>
    public ICommand AddUnitCommand { get; }

    /// <summary>
    /// Command to show unit info for the selected unit
    /// </summary>
    public ICommand ShowUnitInfoCommand { get; }

    /// <summary>Clears the roster search without changing the weight-class filter.</summary>
    public ICommand ClearSearchCommand { get; }

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

    private int GetBattleValue(UnitData unit)
    {
        try
        {
            return Math.Max(0, _mechFactory.Create(unit).CalculateBattleValue());
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Unable to calculate Battle Value for {unit.Chassis} {unit.Model}: {exception}");
            return int.MaxValue;
        }
    }

    private static string GetEra(UnitData unit)
    {
        if (unit.AdditionalAttributes is null)
            return string.Empty;

        return unit.AdditionalAttributes.FirstOrDefault(attribute =>
            attribute.Key.Equals("era", StringComparison.OrdinalIgnoreCase)
            || attribute.Key.Equals("introduction-era", StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
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

    private async Task ShowUnitInfo()
    {
        if (!CanShowUnitInfo || _selectedUnit == null) return;

        var infoViewModel = new UnitInfoViewModel(_selectedUnit.Value, null, _mechFactory);
        await NavigationService.ShowViewModelForResultAsync<UnitInfoViewModel, PilotEditResult?>(infoViewModel);
    }

    private Task ClearSearch()
    {
        SearchText = string.Empty;
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
