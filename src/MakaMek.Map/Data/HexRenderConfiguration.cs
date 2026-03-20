namespace Sanet.MakaMek.Map.Data;

public readonly record struct HexRenderConfiguration(
    bool ShowLabels,
    bool ShowOutline)
{
    public static HexRenderConfiguration Default => new(true, true);
}
