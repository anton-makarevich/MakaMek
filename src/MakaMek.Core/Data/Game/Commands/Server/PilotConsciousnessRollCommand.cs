using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record struct PilotConsciousnessRollCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public required Guid UnitId { get; init; }
    public required Guid PilotId { get; init; }
    public required int ConsciousnessNumber { get; init; }
    public required List<int> DiceResults { get; init; }
    public required bool IsSuccessful { get; init; }
    public required bool IsRecoveryAttempt { get; init; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var pilot = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.UnitId)?.Pilot;

        if (pilot == null)
        {
            return string.Empty;
        }

        var rollTypeKey = IsRecoveryAttempt ? "Command_PilotConsciousnessRoll_Recovery" : "Command_PilotConsciousnessRoll_Consciousness";
        var rollType = localizationService.GetString(rollTypeKey);

        var resultKey = IsSuccessful ? "Command_PilotConsciousnessRoll_Success" : "Command_PilotConsciousnessRoll_Failure";
        var diceText = string.Join(", ", DiceResults);
        var total = DiceResults.Sum();

        var template = localizationService.GetString(resultKey);
        return string.Format(template, pilot.Name, rollType, diceText, total, ConsciousnessNumber);
    }
}
