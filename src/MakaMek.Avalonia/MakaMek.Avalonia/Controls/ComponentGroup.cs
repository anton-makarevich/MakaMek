using System.Collections.Generic;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Avalonia.Controls;

public class ComponentGroup
{
    public string MountedOn { get; set; } = string.Empty;
    public List<Component> Components { get; set; } = [];
}