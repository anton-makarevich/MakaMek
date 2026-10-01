using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Avalonia.Controls;

public partial class UnitRecordSheet : UserControl
{
    public static readonly StyledProperty<Unit?> UnitProperty =
        AvaloniaProperty.Register<UnitRecordSheet, Unit?>(nameof(Unit));

    public static readonly StyledProperty<bool> HasPilotProperty =
        AvaloniaProperty.Register<UnitRecordSheet, bool>(nameof(HasPilot));

    static UnitRecordSheet()
    {
        UnitProperty.Changed.AddClassHandler<UnitRecordSheet>((sender, _) => sender.OnUnitChanged());
    }

    public static readonly StyledProperty<bool> ShowHeatLevelPanelProperty =
        AvaloniaProperty.Register<UnitRecordSheet, bool>(nameof(ShowHeatLevelPanel));

    public static readonly StyledProperty<bool> ShowEventsTabProperty =
        AvaloniaProperty.Register<UnitRecordSheet, bool>(nameof(ShowEventsTab));

    public static readonly StyledProperty<object?> HeatProjectionProperty =
        AvaloniaProperty.Register<UnitRecordSheet, object?>(nameof(HeatProjection));

    public static readonly StyledProperty<PilotViewModel?> EditablePilotProperty =
        AvaloniaProperty.Register<UnitRecordSheet, PilotViewModel?>(nameof(EditablePilot));

    public static readonly StyledProperty<bool> CanEditProperty =
        AvaloniaProperty.Register<UnitRecordSheet, bool>(nameof(CanEdit));

    public static readonly StyledProperty<ICommand?> SaveCommandProperty =
        AvaloniaProperty.Register<UnitRecordSheet, ICommand?>(nameof(SaveCommand));

    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<UnitRecordSheet, ICommand?>(nameof(CancelCommand));

    /// <summary>
    /// Command used to center the map on the unit shown by this sheet.
    /// </summary>
    public static readonly StyledProperty<ICommand?> FocusCommandProperty =
        AvaloniaProperty.Register<UnitRecordSheet, ICommand?>(nameof(FocusCommand));

    /// <summary>
    /// Command used to pin or unpin the surrounding drawer.
    /// </summary>
    public static readonly StyledProperty<ICommand?> PinCommandProperty =
        AvaloniaProperty.Register<UnitRecordSheet, ICommand?>(nameof(PinCommand));

    public static readonly StyledProperty<bool> IsPinnedProperty =
        AvaloniaProperty.Register<UnitRecordSheet, bool>(nameof(IsPinned));

    public static readonly StyledProperty<string?> EditableNameProperty =
        AvaloniaProperty.Register<UnitRecordSheet, string?>(nameof(EditableName));

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

    public PilotViewModel? EditablePilot
    {
        get => GetValue(EditablePilotProperty);
        set => SetValue(EditablePilotProperty, value);
    }

    public bool CanEdit
    {
        get => GetValue(CanEditProperty);
        set => SetValue(CanEditProperty, value);
    }

    public ICommand? SaveCommand
    {
        get => GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command that locates the inspected unit on the map.
    /// </summary>
    public ICommand? FocusCommand
    {
        get => GetValue(FocusCommandProperty);
        set => SetValue(FocusCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command that toggles drawer pinning.
    /// </summary>
    public ICommand? PinCommand
    {
        get => GetValue(PinCommandProperty);
        set => SetValue(PinCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the surrounding drawer is pinned open.
    /// </summary>
    public bool IsPinned
    {
        get => GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    public string? EditableName
    {
        get => GetValue(EditableNameProperty);
        set => SetValue(EditableNameProperty, value);
    }

    public UnitRecordSheet()
    {
        InitializeComponent();
    }

    private void OnUnitChanged()
    {
        var pilot = Unit?.Pilot;
        HasPilot = pilot is not null;
        if (pilot is not null)
        {
            if (EditablePilot?.Pilot != pilot)
            {
                EditablePilot = new PilotViewModel(pilot);
            }
        }
        else
        {
            EditablePilot = null;
        }
    }
}
