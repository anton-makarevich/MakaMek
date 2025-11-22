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

    public TargetSelectionViewModel(IUnit target,
        bool isPrimary,
        bool hasWeaponsForTarget,
        Action<IUnit> onSetPrimary)
    {
        Target = target;
        HasWeaponsForTarget = hasWeaponsForTarget;
        _isPrimary = isPrimary;
        SetAsPrimary = new AsyncCommand(() =>
        {
            onSetPrimary(Target);
            return Task.CompletedTask;
        });
    }

    public IUnit Target { get; }

    public bool HasWeaponsForTarget
    {
        get;
        set => SetProperty(ref field, value);
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

