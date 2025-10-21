using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public record struct JoinGameCommand: IClientCommand
{
    public required string PlayerName { get; init; }
    public required List<UnitData> Units { get; init; }
    public required List<PilotAssignmentData> PilotAssignments { get; init; }
    public required string Tint { get; init; }
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? IdempotencyKey { get; init; }
    public Guid? UnitId { get; init; } //remains null for this command

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var localizedTemplate = localizationService.GetString("Command_JoinGame"); 
        return string.Format(localizedTemplate, PlayerName, Units.Count);
    }

    public Guid PlayerId { get; init; }
}