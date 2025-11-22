using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public abstract class GamePhase : IGamePhase
{
    protected readonly ServerGame Game;

    protected GamePhase(ServerGame game)
    {
        Game = game;
    }

    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }

    public abstract void HandleCommand(IGameCommand command);
    public abstract PhaseNames Name { get; }

    /// <summary>
    /// Processes consciousness rolls for a unit's pilot
    /// </summary>
    /// <param name="unit">The unit whose pilot needs consciousness rolls</param>
    protected void ProcessConsciousnessRollsForUnit(IUnit unit)
    {
        if (unit.Pilot == null) return;

        var consciousnessCommands = Game.ConsciousnessCalculator.MakeConsciousnessRolls(unit.Pilot);

        foreach (var command in consciousnessCommands)
        {
            var broadcastCommand = command;
            broadcastCommand.GameOriginId = Game.Id;
            Game.OnPilotConsciousnessRoll(broadcastCommand);
            Game.CommandPublisher.PublishCommand(broadcastCommand);
        }
    }
}
