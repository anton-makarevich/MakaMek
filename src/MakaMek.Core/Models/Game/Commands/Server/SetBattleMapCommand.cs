using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Server;

/// <summary>
/// Command sent from server to clients to set the battle map
/// </summary>
public record struct SetBattleMapCommand : IGameCommand
{
    public Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Format(ILocalizationService localizationService, IGame game)
    {
        var localizedTemplate = localizationService.GetString("Command_SetBattleMap");
        return string.Format(localizedTemplate);
    }

    public required List<HexData> MapData { get; init; }
}
