using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Avalonia.Controls;

public partial class UnitComponentsPanel : UserControl
{
    public UnitComponentsPanel()
    {
        InitializeComponent();
    }

    public static readonly DirectProperty<UnitComponentsPanel, Unit?> UnitProperty =
        AvaloniaProperty.RegisterDirect<UnitComponentsPanel, Unit?>(
            nameof(Unit),
            o => o.Unit,
            (o, v) => o.Unit = v);

    private Unit? _unit;
    public Unit? Unit
    {
        get => _unit;
        set
        {
            SetAndRaise(UnitProperty, ref _unit, value);
            UpdateComponentsList();
        }
    }

    private void UpdateComponentsList()
    {
        if (Unit == null)
        {
            ComponentsGroup.ItemsSource = null;
            return;
        }

        var components = Unit.Parts.Values
            .SelectMany(p => p.Components)
            .ToList();

        var grouped = components
            .GroupBy(c => c.MountedOn[0])
            .Select(g => new ComponentGroup
            {
                MountedOn = g.Key.Name,
                Components = g.ToList()
            })
            .ToList();

        ComponentsGroup.ItemsSource = grouped;
    }
}
