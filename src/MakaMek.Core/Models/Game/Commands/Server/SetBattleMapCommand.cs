using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Server;

/// <summary>
/// Command sent from server to clients to set the battle map
/// </summary>
public class SetBattleMapCommand : IGameCommand
{
    public Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Format(ILocalizationService localizationService, IGame game)
    {
        throw new NotImplementedException();
    }

    public required List<HexData> MapData { get; init; } = [];
}
