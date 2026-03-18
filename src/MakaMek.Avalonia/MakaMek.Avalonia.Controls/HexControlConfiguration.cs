namespace Sanet.MakaMek.Avalonia.Controls;

public readonly record struct HexControlConfiguration(
    bool ShowLabels,
    bool ShowOutline)
{
    public static HexControlConfiguration Default => new(true, true);
}
