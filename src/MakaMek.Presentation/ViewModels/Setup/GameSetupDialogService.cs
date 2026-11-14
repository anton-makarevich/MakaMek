using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.Services;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Runs the setup dialogs (unit picker and pilot editor) and exposes only their results. Kept out of
/// the setup session so it does not depend on the dialog view models.
/// </summary>
public sealed class GameSetupDialogService
{
    private readonly IMechFactory _mechFactory;

    public GameSetupDialogService(IMechFactory mechFactory)
    {
        _mechFactory = mechFactory;
    }

    /// <summary>
    /// Shows the available-units dialog and returns the picked unit, or <c>null</c> when cancelled.
    /// </summary>
    public async Task<UnitData?> ShowAvailableUnitsAsync(
        INavigationService navigationService,
        IList<UnitData> availableUnits,
        bool canAddUnit)
    {
        if (!canAddUnit) return null;

        var tableViewModel = new AvailableUnitsTableViewModel(availableUnits, _mechFactory);
        tableViewModel.SetNavigationService(navigationService);

        var result = await navigationService.ShowViewModelForResultAsync<AvailableUnitsTableViewModel, UnitSelectionResult>(tableViewModel);
        return result.SelectedUnit;
    }

    /// <summary>
    /// Shows the unit info/pilot-edit dialog.
    /// </summary>
    public Task<PilotEditResult?> ShowUnitInfoAsync(
        INavigationService navigationService,
        UnitData unitData,
        PilotData? pilotData,
        bool canEdit)
    {
        var infoViewModel = new UnitInfoViewModel(unitData, pilotData, _mechFactory, canEdit);
        infoViewModel.SetNavigationService(navigationService);
        return navigationService.ShowViewModelForResultAsync<UnitInfoViewModel, PilotEditResult?>(infoViewModel);
    }
}
