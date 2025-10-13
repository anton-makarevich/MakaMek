using Sanet.MakaMek.Core.Models.Units;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// ViewModel for displaying heat projection based on selected weapons
/// </summary>
public class HeatProjectionViewModel : BindableBase
{
    private Unit? _attacker;
    private int _projectedHeat;

    /// <summary>
    /// Gets or sets the attacking unit
    /// </summary>
    public Unit? Attacker
    {
        get => _attacker;
        set
        {
            SetProperty(ref _attacker, value);
            NotifyPropertyChanged(nameof(CurrentHeat));
            NotifyPropertyChanged(nameof(HeatDissipation));
            UpdateProjectedHeat();
        }
    }

    /// <summary>
    /// Gets the current heat level of the attacker
    /// </summary>
    public int CurrentHeat => Attacker?.CurrentHeat ?? 0;

    /// <summary>
    /// Gets the projected heat level after firing selected weapons
    /// </summary>
    public int ProjectedHeat
    {
        get => _projectedHeat;
        private set => SetProperty(ref _projectedHeat, value);
    }

    /// <summary>
    /// Gets the heat dissipation capacity of the attacker
    /// </summary>
    public int HeatDissipation => Attacker?.HeatDissipation ?? 0;

    /// <summary>
    /// Updates the projected heat based on currently selected weapons
    /// </summary>
    public void UpdateProjectedHeat()
    {
        if (Attacker == null)
        {
            ProjectedHeat = 0;
            return;
        }

        var selectedWeaponsHeat = Attacker.WeaponAttackState.SelectedWeapons
            .Sum(weapon => weapon.Heat);

        ProjectedHeat = CurrentHeat + selectedWeaponsHeat;
        
        // Also notify CurrentHeat in case the unit's heat changed
        NotifyPropertyChanged(nameof(CurrentHeat));
        NotifyPropertyChanged(nameof(HeatDissipation));
    }
}

