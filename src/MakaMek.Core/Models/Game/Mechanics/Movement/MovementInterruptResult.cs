namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

public class MovementInterruptResult
{
    /// <summary>
    /// If true, movement stops — no further segments or handlers are checked.
    /// If false, the handler produced PSR commands but movement continues.
    /// </summary>
    public bool ShouldStop { get; init; }

    /// <summary>
    /// If true, the phase defers step consumption (for standup after water fall).
    /// Only meaningful when ShouldStop is true.
    /// </summary>
    public bool DeferStepConsumption { get; init; }

    /// <summary>
    /// Ordered list of game state mutations and publish actions to apply.
    /// The phase executes these in order, collecting and publishing commands.
    /// </summary>
    public IReadOnlyList<IGameAction> GameActions { get; init; } = [];
}
