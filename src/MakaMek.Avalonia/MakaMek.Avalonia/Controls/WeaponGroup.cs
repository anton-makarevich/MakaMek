using System.Collections.Generic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Avalonia.Controls;

public class WeaponGroup
{
    public string MountedOn { get; set; } = string.Empty;
    public List<Weapon> Weapons { get; set; } = [];
}