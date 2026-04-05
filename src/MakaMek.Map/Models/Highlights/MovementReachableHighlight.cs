using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.Highlights;

/// <summary>
/// Highlight for hexes reachable during movement phase.
/// Rendered with light blue stroke/fill.
/// </summary>
public record MovementReachableHighlight(MovementType MovementType) : IHexHighlightType
{
    public int RenderOrder => 0;
    public string Name => nameof(MovementReachableHighlight);

    public string Render(ILocalizationService localizationService) =>
        MovementType switch
        {
            MovementType.Walk => localizationService.GetString("MovementType_Walk"),
            MovementType.Run => localizationService.GetString("MovementType_Run"),
            MovementType.Jump => localizationService.GetString("MovementType_Jump"),
            _ => MovementType.ToString()
        };
}
