using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public abstract class GamePhase : IGamePhase
{
    protected ServerGame Game { get; }

    protected GamePhase(ServerGame game)
    {
        Game = game;
    }

    public virtual void Enter()
    {
    }

    public virtual void ExitPhase()
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
        Game.ProcessConsciousnessRolls(unit);
    }
}
