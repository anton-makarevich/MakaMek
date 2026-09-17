using Avalonia.Controls;
using System.Windows.Input;
using Avalonia;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// Compact clickable HUD card for one unit in the local player's squad.
/// </summary>
public partial class UnitStatusBarItem : UserControl
{
    public static readonly StyledProperty<ICommand?> FocusCommandProperty =
        AvaloniaProperty.Register<UnitStatusBarItem, ICommand?>(nameof(FocusCommand));

    /// <summary>
    /// Gets or sets the command that centers the map on this unit.
    /// </summary>
    public ICommand? FocusCommand
    {
        get => GetValue(FocusCommandProperty);
        set => SetValue(FocusCommandProperty, value);
    }

    public UnitStatusBarItem()
    {
        InitializeComponent();
    }
}
