using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Avalonia.Controls;

public partial class UnitWeaponsPanel : UserControl
{
    public UnitWeaponsPanel()
    {
        InitializeComponent();
    }

    public static readonly DirectProperty<UnitWeaponsPanel, Unit?> UnitProperty =
        AvaloniaProperty.RegisterDirect<UnitWeaponsPanel, Unit?>(
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
            UpdateWeaponsList();
        }
    }



    private void UpdateWeaponsList()
    {
        if (Unit == null)
        {
            WeaponsGroup.ItemsSource = null;
            return;
        }

        var weapons = Unit.Parts
            .SelectMany(p => p.GetComponents<Weapon>())
            .ToList();

        var grouped = weapons
            .GroupBy(w => w.MountedOn)
            .Select(g => new WeaponGroup
            {
                MountedOn = g.Key?.Name??"NA",
                Weapons = g.ToList()
            })
            .ToList();

        WeaponsGroup.ItemsSource = grouped;
    }
}