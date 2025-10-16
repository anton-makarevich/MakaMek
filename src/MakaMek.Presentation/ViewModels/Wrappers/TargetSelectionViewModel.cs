using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// View model wrapper for a target in the weapon selection panel
/// </summary>
public class TargetSelectionViewModel : BindableBase
{
    private bool _isPrimary;
    private bool _hasWeaponsForTarget;

    public TargetSelectionViewModel(Unit target,
        bool isPrimary,
        bool hasWeaponsForTarget,
        Action<Unit> onSetPrimary)
    {
        Target = target;
        HasWeaponsForTarget = hasWeaponsForTarget;
        _isPrimary = isPrimary;
        var onSetPrimary1 = onSetPrimary;
        SetAsPrimary = new AsyncCommand(() =>
        {
            onSetPrimary1(Target);
            return Task.CompletedTask;
        });
    }

    public Unit Target { get; }

    public bool HasWeaponsForTarget
    {
        get => _hasWeaponsForTarget;
        set => SetProperty(ref _hasWeaponsForTarget, value);
    }

    public string Name => Target.Name;

    public bool IsPrimary
    {
        get => _isPrimary;
        set
        {
            SetProperty(ref _isPrimary, value);
            NotifyPropertyChanged(nameof(IsSecondary));
        }
    }

    public bool IsSecondary => !IsPrimary;

    public ICommand SetAsPrimary { get; }
}

