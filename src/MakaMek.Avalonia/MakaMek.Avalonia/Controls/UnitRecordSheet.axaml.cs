using Avalonia;
using Avalonia.Controls;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Avalonia.Controls;

public partial class UnitRecordSheet : UserControl
{
    public static readonly StyledProperty<Unit?> UnitProperty =
        AvaloniaProperty.Register<UnitRecordSheet, Unit?>(nameof(Unit));

    public static readonly StyledProperty<bool> HasPilotProperty =
        AvaloniaProperty.Register<UnitRecordSheet, bool>(nameof(HasPilot), defaultValue: true);

    public static readonly StyledProperty<bool> ShowHeatLevelPanelProperty =
        AvaloniaProperty.Register<UnitRecordSheet, bool>(nameof(ShowHeatLevelPanel));

    public static readonly StyledProperty<bool> ShowEventsTabProperty =
        AvaloniaProperty.Register<UnitRecordSheet, bool>(nameof(ShowEventsTab));

    public static readonly StyledProperty<object?> HeatProjectionProperty =
        AvaloniaProperty.Register<UnitRecordSheet, object?>(nameof(HeatProjection));

    public Unit? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public bool HasPilot
    {
        get => GetValue(HasPilotProperty);
        set => SetValue(HasPilotProperty, value);
    }

    public bool ShowHeatLevelPanel
    {
        get => GetValue(ShowHeatLevelPanelProperty);
        set => SetValue(ShowHeatLevelPanelProperty, value);
    }

    public bool ShowEventsTab
    {
        get => GetValue(ShowEventsTabProperty);
        set => SetValue(ShowEventsTabProperty, value);
    }

    public object? HeatProjection
    {
        get => GetValue(HeatProjectionProperty);
        set => SetValue(HeatProjectionProperty, value);
    }

    public UnitRecordSheet()
    {
        InitializeComponent();
    }
}
