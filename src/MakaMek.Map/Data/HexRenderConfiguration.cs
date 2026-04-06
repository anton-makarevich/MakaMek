namespace Sanet.MakaMek.Map.Data;

public readonly record struct HexRenderConfiguration(
    bool ShowLabels,
    bool ShowOutline,
    bool ShowHighlightLabels)
{
    public static HexRenderConfiguration Default => new(
        true,
        true,
        false);
}
