using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Command sent from server to clients to set the battle map
/// </summary>
public record struct SetBattleMapCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Render(ILocalizationService localizationService, IGame game)
    {
        return string.Format(localizationService.GetString("Command_SetBattleMap"));
    }

    public required List<HexData> MapData { get; init; }
}
