using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// ViewModel for displaying heat projection based on selected weapons or declared attacks
/// </summary>
public class HeatProjectionViewModel : BindableBase
{
    private Unit? _unit;
    private int _projectedHeat;
    private readonly ILocalizationService _localizationService;

    public HeatProjectionViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    /// <summary>
    /// Gets or sets the unit
    /// </summary>
    public Unit? Unit
    {
        get => _unit;
        set
        {
            SetProperty(ref _unit, value);
            NotifyPropertyChanged(nameof(CurrentHeat));
            NotifyPropertyChanged(nameof(HeatDissipation));
            UpdateProjectedHeat();
        }
    }

    /// <summary>
    /// Gets the current heat level of the unit
    /// </summary>
    public int CurrentHeat => Unit?.CurrentHeat ?? 0;

    /// <summary>
    /// Gets the projected heat level after firing selected weapons
    /// </summary>
    public int ProjectedHeat
    {
        get => _projectedHeat;
        private set => SetProperty(ref _projectedHeat, value);
    }

    /// <summary>
    /// Gets the heat dissipation capacity of the unit
    /// </summary>
    public int HeatDissipation => Unit?.HeatDissipation ?? 0;

    /// <summary>
    /// Updates the projected heat based on currently selected weapons
    /// </summary>
    public void UpdateProjectedHeat()
    {
        if (Unit == null)
        {
            ProjectedHeat = 0;
            return;
        }

        var selectedWeaponsHeat = Unit.WeaponAttackState.SelectedWeapons
            .Sum(weapon => weapon.Heat);

        ProjectedHeat = CurrentHeat + selectedWeaponsHeat;
        
        // Also notify CurrentHeat in case the unit's heat changed
        NotifyPropertyChanged(nameof(CurrentHeat));
        NotifyPropertyChanged(nameof(HeatDissipation));
        NotifyPropertyChanged(nameof(HeatProjectionText));
        NotifyPropertyChanged(nameof(HeatDissipationText));
    }
    
    public string HeatProjectionText 
        => string.Format(_localizationService.GetString("HeatProjection_ProjectionText"), CurrentHeat, ProjectedHeat);
    public string HeatDissipationText 
        => string.Format(_localizationService.GetString("HeatProjection_DissipationText"), HeatDissipation);
}

