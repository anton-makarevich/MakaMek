using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Shared consciousness-roll processing, usable by phases and by extracted resolution
/// collaborators that are not derived from <see cref="GamePhase"/>.
/// </summary>
internal static class ConsciousnessRolls
{
    /// <summary>
    /// Processes consciousness rolls for a unit's pilot
    /// </summary>
    /// <param name="game">The authoritative game</param>
    /// <param name="unit">The unit whose pilot needs consciousness rolls</param>
    public static void Process(ServerGame game, IUnit unit)
    {
        if (unit.Pilot == null) return;

        var consciousnessCommands = game.ConsciousnessCalculator.MakeConsciousnessRolls(unit.Pilot);

        foreach (var command in consciousnessCommands)
        {
            var broadcastCommand = command;
            broadcastCommand.GameOriginId = game.Id;
            game.OnPilotConsciousnessRoll(broadcastCommand);
            game.CommandPublisher.PublishCommand(broadcastCommand);
        }
    }
}
