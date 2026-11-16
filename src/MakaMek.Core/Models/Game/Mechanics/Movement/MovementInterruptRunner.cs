using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

/// <summary>
/// The outcome of a movement interrupt check: whether the move stops at the interrupt and whether
/// the step after the fall still has to be consumed.
/// </summary>
internal readonly record struct MovementInterruptOutcome(bool ShouldStop, bool DeferStepConsumption);

/// <summary>
/// Runs the movement interrupt handlers for a move and publishes the commands their game actions
/// produce. Extracted from <see cref="Phases.MovementPhase"/> so the phase only decides when to run
/// the checks and how to react to their outcome.
/// </summary>
internal sealed class MovementInterruptRunner
{
    private readonly ServerGame _game;
    private readonly IReadOnlyList<IMovementInterruptHandler> _segmentHandlers;
    private readonly IReadOnlyList<IMovementInterruptHandler> _landingHandlers;

    public MovementInterruptRunner(ServerGame game, DominoEffectResolver dominoEffectResolver)
    {
        _game = game;
        _segmentHandlers =
        [
            new BridgeCollapseInterruptHandler(new BridgeCollapseResolver(dominoEffectResolver)),
            new SkidInterruptHandler(),
            new WaterEntryInterruptHandler(),
            new RubbleEntryInterruptHandler()
        ];

        _landingHandlers =
        [
            new BridgeCollapseInterruptHandler(new BridgeCollapseResolver(dominoEffectResolver)),
            new JumpDamageInterruptHandler(),
            new WaterEntryInterruptHandler(),
            new RubbleEntryInterruptHandler()
        ];
    }

    /// <summary>
    /// Checks every segment of a walk/run move against the segment handlers.
    /// </summary>
    /// <returns>The stopping outcome, or null when every segment was clean.</returns>
    public MovementInterruptOutcome? RunSegmentInterrupts(IUnit unit, MoveUnitCommand command)
    {
        for (var i = 0; i < command.MovementPath.Count; i++)
        {
            var context = new MovementInterruptContext
            {
                MoveCommand = command,
                SegmentIndex = i,
                Unit = unit,
                Game = _game
            };

            var outcome = RunHandlers(_segmentHandlers, context);
            if (outcome is not null) return outcome;
        }

        return null;
    }

    /// <summary>
    /// Checks the landing hex of a jump against the landing handlers.
    /// </summary>
    /// <returns>The stopping outcome, or null when the landing was clean.</returns>
    public MovementInterruptOutcome? RunLandingInterrupts(IUnit unit, MoveUnitCommand command)
    {
        var context = new MovementInterruptContext
        {
            MoveCommand = command,
            SegmentIndex = command.MovementPath.Count - 1,
            Unit = unit,
            Game = _game,
            IsLandingCheck = true
        };

        return RunHandlers(_landingHandlers, context);
    }

    private MovementInterruptOutcome? RunHandlers(
        IReadOnlyList<IMovementInterruptHandler> handlers,
        MovementInterruptContext context)
    {
        foreach (var handler in handlers)
        {
            var result = handler.Check(context);
            if (result == null) continue;

            var outcome = Apply(result, context.Unit);
            if (outcome.ShouldStop) return outcome;
        }

        return null;
    }

    private MovementInterruptOutcome Apply(MovementInterruptResult result, IUnit unit)
    {
        var commands = new List<IGameCommand>();
        bool? deferAfterFall = null;

        foreach (var action in result.GameActions)
        {
            commands.AddRange(action.Process(_game));
            if (unit is Mech fallingMech && action is ApplyFallAction)
                deferAfterFall = fallingMech.CanStandup();
            if (action is ApplySkidAction)
                deferAfterFall = false;
        }

        foreach (var command in commands)
            _game.CommandPublisher.PublishCommand(command);

        return new MovementInterruptOutcome(result.ShouldStop, deferAfterFall ?? result.DeferStepConsumption);
    }
}
