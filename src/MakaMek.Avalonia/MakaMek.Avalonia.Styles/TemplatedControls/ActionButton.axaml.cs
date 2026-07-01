using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Sanet.MakaMek.Avalonia.Styles.TemplatedControls;

public class ActionButton : Button
{
    public static readonly StyledProperty<Geometry?> IconDataProperty = AvaloniaProperty.Register<ActionButton, Geometry?>(
        nameof(IconData));

    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }
}
