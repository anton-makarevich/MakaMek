using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Executes the displacement actions of the physical-attack phase (push, charge and their domino
/// chains) and the automatic fall that a displacement can trigger.
/// </summary>
internal sealed class PhysicalAttackDisplacementService
{
    private readonly ServerGame _game;

    public PhysicalAttackDisplacementService(ServerGame game)
    {
        _game = game;
    }

    public IEnumerable<IUnit> AllUnits() => _game.Players.SelectMany(player => player.Units);

    public void Execute(IGameAction action)
    {
        foreach (var command in action.Process(_game))
            _game.CommandPublisher.PublishCommand(command);
    }

    public void ExecuteDisplacement(IUnit unit, HexPosition destination, DisplacementReason reason)
    {
        if (unit.Position is not { } from) return;

        Execute(new DisplaceUnitAction(
            new DisplaceUnitCommand
            {
                UnitId = unit.Id,
                FromCoordinates = from.Coordinates.ToData(),
                ToCoordinates = destination.Coordinates.ToData(),
                NewFacing = (int)destination.Facing,
                Surface = (int)destination.Surface,
                DisplacementReason = reason,
                GameOriginId = _game.Id,
                Timestamp = DateTime.UtcNow
            },
            publish: true));
    }

    /// <summary>
    /// A push or charge that drops the target more than two levels is allowed and ends in an
    /// automatic fall (Total Warfare, p. 148, 151). The fall lands in the destination hex; a unit
    /// that becomes prone there skips its later PushHit roll.
    /// </summary>
    public void ResolveAutomaticFall(Mech mech, int levelsFallen)
    {
        if (mech.Position is not { } position) return;

        var facingRoll = _game.DiceRoller.RollD6();
        var facingAfterFall = _game.RulesProvider.GetFacingAfterFall(facingRoll.Result, position.Facing);
        var fallContext = _game.FallProcessor.ProcessMovementAttempt(
            mech,
            new CliffFallRollContext(levelsFallen, facingRoll, facingAfterFall),
            _game,
            MovementType.StandingStill,
            position);

        if (fallContext.IsFalling)
            Execute(new ApplyFallAction(mech, fallContext.ToMechFallCommand()));
    }
}
