using Sanet.MakaMek.Core.Models.Game.Rules;
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
    private readonly IRulesProvider _rulesProvider;

    public HeatProjectionViewModel(ILocalizationService localizationService, IRulesProvider rulesProvider)
    {
        _localizationService = localizationService;
        _rulesProvider = rulesProvider;
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
    /// Updates the projected heat based on currently selected weapons or declared attacks
    /// </summary>
    public void UpdateProjectedHeat()
    {
        if (Unit == null)
        {
            ProjectedHeat = 0;
            return;
        }
        
        if (Unit.HasAppliedHeat)
        {
            ProjectedHeat = CurrentHeat;
        }
        else
        {
            var heatData = Unit.GetHeatData(_rulesProvider);

            var movementHeat =
                heatData.MovementHeatSources.Sum(source => source.HeatPoints);

            // Use declared weapon heat if attacks have been declared, otherwise use selected weapons
            var weaponHeat = Unit.HasDeclaredWeaponAttack
                ? heatData.WeaponHeatSources.Sum(source => source.HeatPoints)
                : Unit.WeaponAttackState.SelectedWeapons.Sum(weapon => weapon.Heat);

            // Include engine heat penalty if present
            var engineHeat = heatData.EngineHeatSource?.Value ?? 0;

            ProjectedHeat = CurrentHeat + movementHeat + weaponHeat + engineHeat;
        }

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

