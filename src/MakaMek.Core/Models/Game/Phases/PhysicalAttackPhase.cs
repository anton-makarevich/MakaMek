using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Physical attack declarations. Only units with at least one physical attack option declare, and
/// a player with none of them is skipped without being made active. Each declaring unit sends one
/// <see cref="PhysicalAttackCommand"/>; an empty one means no attack.
/// </summary>
public class PhysicalAttackPhase(ServerGame game) : MainGamePhase(game)
{
    private readonly HashSet<Guid> _unitsToDeclare = [];

    /// <summary>
    /// Temporary: while false, the phase advances as soon as it is entered, because no client can
    /// declare a physical attack yet and the phase would wait forever. The UI ticket (#1506)
    /// switches it on and then removes it.
    /// </summary>
    internal bool IsDeclarationEnabled { get; init; }

    public override void Enter()
    {
        if (!IsDeclarationEnabled)
        {
            Game.TransitionToNextPhase(Name);
            return;
        }

        FindUnitsToDeclare();
        base.Enter();
    }

    public override void Exit()
    {
        _unitsToDeclare.Clear();
        base.Exit();
    }

    protected override void CalculateTurnOrder(TurnOrder turnOrder) =>
        turnOrder.CalculateOrder(Game.InitiativeOrder,
            player => player.AliveUnits.Count(unit => _unitsToDeclare.Contains(unit.Id)));

    public override void HandleCommand(IGameCommand command)
    {
        if (command is not PhysicalAttackCommand attackCommand) return;

        if (Validate(attackCommand) is { } errorCode)
        {
            Game.Logger.LogWarning("Rejecting physical attack declaration for unit {UnitId}: {ErrorCode}",
                attackCommand.UnitId, errorCode);
            Game.CommandPublisher.PublishCommand(new ErrorCommand
            {
                GameOriginId = Game.Id,
                IdempotencyKey = attackCommand.IdempotencyKey,
                ErrorCode = errorCode,
                Timestamp = DateTime.UtcNow
            });
            return;
        }

        HandleUnitAction(command, attackCommand.PlayerId);
    }

    protected override void ProcessCommand(IGameCommand command)
    {
        var attackCommand = (PhysicalAttackCommand)command;
        _unitsToDeclare.Remove(attackCommand.UnitId);
        var broadcastCommand = attackCommand;
        broadcastCommand.GameOriginId = Game.Id;
        Game.OnPhysicalAttack(attackCommand);
        Game.CommandPublisher.PublishCommand(broadcastCommand);
    }

    public override PhaseNames Name => PhaseNames.PhysicalAttack;

    /// <summary>
    /// Settles at the start of the phase which units declare, so units lost during weapon
    /// resolution are already out. Players left with none are logged and skipped.
    /// </summary>
    private void FindUnitsToDeclare()
    {
        _unitsToDeclare.Clear();
        var map = Game.BattleMap ?? throw new InvalidOperationException("Battle map is null");
        var allUnits = Game.Players.SelectMany(p => p.Units).ToList();

        foreach (var player in Game.InitiativeOrder)
        {
            var declaringUnits = player.AliveUnits
                .Where(unit => Game.PhysicalAttackEligibility.HasAnyOption(unit, allUnits, map))
                .ToList();
            foreach (var unit in declaringUnits)
                _unitsToDeclare.Add(unit.Id);

            if (declaringUnits.Count == 0)
                Game.Logger.LogInformation(
                    "Skipping {PlayerName} in the physical attack phase: no unit has a physical attack option",
                    player.Name);
        }
    }

    /// <summary>
    /// Null when the command is valid. Otherwise the error to send back; nothing is changed.
    /// </summary>
    private ErrorCode? Validate(PhysicalAttackCommand command)
    {
        if (command.PlayerId != Game.PhaseStepState?.ActivePlayer.Id) return ErrorCode.InvalidGameState;

        var attacker = Game.Players
            .FirstOrDefault(p => p.Id == command.PlayerId)?
            .Units.FirstOrDefault(u => u.Id == command.UnitId);
        if (attacker == null || !_unitsToDeclare.Contains(attacker.Id)) return ErrorCode.InvalidGameState;

        if (command.Declaration is not { } declaration) return null;

        var target = Game.Players.SelectMany(p => p.Units).FirstOrDefault(u => u.Id == declaration.TargetId);
        var limbs = declaration.AttackerLimbs;
        if (target == null || limbs is not { Count: > 0 } || limbs.Distinct().Count() != limbs.Count)
            return ErrorCode.ValidationFailed;

        // The player picks the kicking leg; a kick is made with one leg only.
        if (declaration.AttackType == PhysicalAttackType.Kick && limbs.Count != 1) return ErrorCode.ValidationFailed;

        var eligibleLimbs = Game.PhysicalAttackEligibility.GetEligibleLimbs(attacker, target, declaration.AttackType, Game.BattleMap!);
        return limbs.All(eligibleLimbs.Contains) ? null : ErrorCode.ValidationFailed;
    }
}
