using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Publishes the local player's join and ready commands (registering its bot when it is one). Kept out
/// of the setup session so the session does not depend on the command payloads or the bot settings.
/// </summary>
public sealed class LocalPlayerCommands
{
    private readonly IBotManager _botManager;

    public LocalPlayerCommands(IBotManager botManager)
    {
        _botManager = botManager;
    }

    public void PublishJoin(ClientGame game, PlayerViewModel playerVm)
    {
        // Project UnitViewModel wrappers back to UnitData for the join command
        var unitsData = playerVm.GetUnitsData();

        // Create pilot assignments for each unit
        var pilotAssignments = unitsData.Select(unit =>
        {
            var unitId = unit.Id ?? Guid.NewGuid();
            return new PilotAssignmentData
            {
                UnitId = unitId,
                PilotData = playerVm.GetPilotDataForUnit(unitId) ?? PilotData.CreateDefaultPilot("MechWarrior", "")
            };
        }).ToList();

        if (playerVm.Player.ControlType == PlayerControlType.Bot)
        {
            _botManager.AddBot(playerVm.Player, playerVm.GetBotSettings());
        }

        game.JoinGameWithUnits(playerVm.Player, unitsData, pilotAssignments);
    }
}
