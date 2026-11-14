using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Resolves the weapon attacks declared this turn: a queue in initiative order, the pre-attack
/// gates, the resolver, then damage, criticals, consciousness rolls and the end-of-phase piloting
/// skill rolls.
/// </summary>
public class WeaponAttackResolutionPhase : GamePhase
{
    private readonly WeaponAttackResolutionState _state = new();
    private readonly WeaponAttackQueueBuilder _queueBuilder;
    private readonly WeaponAttackApplier _applier;
    private readonly WeaponAttackEndOfPhasePsrCalculator _endOfPhasePsrCalculator;

    private readonly IReadOnlyList<IAttackResolutionGate> _attackResolutionGates =
    [
        new AttackerPartialCoverGate()
    ];

    public WeaponAttackResolutionPhase(ServerGame game) : base(game)
    {
        _queueBuilder = new WeaponAttackQueueBuilder(game);
        _applier = new WeaponAttackApplier(game, _state);
        _endOfPhasePsrCalculator = new WeaponAttackEndOfPhasePsrCalculator(game, _state);
    }

    public override void Enter()
    {
        // Clear any damage accumulated by a previous run of this phase
        _state.Clear();

        if (Game.BattleMap == null)
        {
            throw new Exception("Battle map is null");
        }

        var playersInOrder = Game.InitiativeOrder.ToList();
        var attackQueue = _queueBuilder.Build(playersInOrder);

        foreach (var item in attackQueue)
            ResolveAttack(item);

        _endOfPhasePsrCalculator.Calculate();
        Game.TransitionToNextPhase(Name);
    }

    private void ResolveAttack(WeaponAttackQueueItem item)
    {
        if (item.Weapon is not { } weapon) return;
        if (item.TargetUnit is not { Position: not null } targetUnit) return;
        if (item.Attacker.Position == null) return;

        if (ShouldSkip(weapon, targetUnit, item)) return;

        var resolution = Game.WeaponAttackResolver.ResolveAttack(
            item.Attacker, targetUnit, weapon, item.WeaponTargetData, Game.BattleMap!);
        _applier.Apply(item.Player, item.Attacker, weapon, targetUnit, resolution);
    }

    private bool ShouldSkip(Weapon weapon, IUnit targetUnit, WeaponAttackQueueItem item)
    {
        foreach (var gate in _attackResolutionGates)
        {
            if (gate.ShouldSkip(
                    item.Attacker, targetUnit, weapon, item.PrimaryAssignment,
                    Game.BattleMap!, Game.RulesProvider, Game.Logger))
                return true;
        }

        return false;
    }

    public override void HandleCommand(IGameCommand command)
    {
        // This phase doesn't process incoming commands, but we need to implement this method
    }

    public override PhaseNames Name => PhaseNames.WeaponAttackResolution;
}
