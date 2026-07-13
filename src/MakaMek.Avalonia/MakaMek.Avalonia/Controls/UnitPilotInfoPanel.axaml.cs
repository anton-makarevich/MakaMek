using Avalonia;
using Avalonia.Controls;

namespace Sanet.MakaMek.Avalonia.Controls;

public partial class UnitPilotInfoPanel : UserControl
{
    public static readonly StyledProperty<bool> CanEditProperty =
        AvaloniaProperty.Register<UnitPilotInfoPanel, bool>(nameof(CanEdit));

    public bool CanEdit
    {
        get => GetValue(CanEditProperty);
        set => SetValue(CanEditProperty, value);
    }

    public UnitPilotInfoPanel()
    {
        InitializeComponent();
    }
}
