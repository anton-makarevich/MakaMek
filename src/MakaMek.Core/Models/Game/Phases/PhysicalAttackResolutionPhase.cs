using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Resolves the physical attacks declared this turn, following the weapon attack resolution
/// pattern: a queue in resolution order with one item per declared limb (or one item for a
/// paired-arm attack such as Push) or charge, the pre-attack gates, the resolver, then criticals,
/// consciousness rolls and the end-of-phase piloting skill rolls.
/// </summary>
/// <remarks>
/// The queue is built once, before any damage of the phase, so an attack is never dropped because
/// an earlier one in the same phase destroyed or felled its attacker or its target. Attacks resolve
/// from the lower Initiative to the higher (Total Warfare p. 144): a charge declared during the
/// Movement Phase keeps its attacker's slot instead of always resolving last. Damage is applied
/// attack by attack, as the weapon phase does, because the resolver works out damage transfer from
/// the target's current state: a second hit on a location the first one destroyed has to move
/// inward. Each attack's displacement follows immediately (Total Warfare p. 151); only a mutual
/// push pair waits for both rolls, because if both hit neither unit moves (p. 148).
/// </remarks>
public class PhysicalAttackResolutionPhase : GamePhase
{
    private readonly IReadOnlyList<IPhysicalAttackResolutionGate> _gates;
    private readonly PhysicalAttackPhaseState _state = new();
    private readonly PhysicalAttackResolutionQueueBuilder _queueBuilder;
    private readonly PhysicalAttackSingleResolver _singleResolver;
    private readonly PhysicalAttackChargeResolver _chargeResolver;
    private readonly PhysicalAttackPushResolver _pushResolver;
    private readonly PhysicalAttackEndOfPhasePsrCalculator _endOfPhasePsrCalculator;

    public PhysicalAttackResolutionPhase(
        ServerGame game,
        PushDisplacementResolver pushDisplacementResolver,
        ChargeDisplacementResolver chargeDisplacementResolver) : base(game)
    {
        _gates =
        [
            new AttackerDestroyedGate(),
            new AttackerProneGate(),
            new PhysicalAttackTargetValidityGate(game.PhysicalAttackEligibility)
        ];

        var displacement = new PhysicalAttackDisplacementService(game);
        var consequences = new PhysicalAttackConsequenceApplier(game, _state);
        _queueBuilder = new PhysicalAttackResolutionQueueBuilder(game, _gates);
        _singleResolver = new PhysicalAttackSingleResolver(game, consequences);
        _chargeResolver = new PhysicalAttackChargeResolver(
            game, chargeDisplacementResolver, displacement, consequences);
        _pushResolver = new PhysicalAttackPushResolver(
            game, _state, pushDisplacementResolver, displacement);
        _endOfPhasePsrCalculator = new PhysicalAttackEndOfPhasePsrCalculator(game, _state);
    }

    public override void Enter()
    {
        base.Enter();
        _state.Clear();

        // The queue is snapshotted before any damage: a charge must still resolve when an earlier
        // attack of the phase destroys its attacker or target, and Unit clears a destroyed unit's
        // pending declaration.
        var queue = _queueBuilder.Build();
        var reciprocalPushes = PhysicalAttackPushResolver.GetReciprocalPushPairs(queue);
        List<PushOutcome> deferredPushes = [];

        foreach (var item in queue)
        {
            if (item.Charge is { } charge)
            {
                _chargeResolver.Resolve(charge);
                continue;
            }

            var attack = item.Attack!.Value;
            var resolution = _singleResolver.Resolve(attack);
            if (attack.Context.AttackType != PhysicalAttackType.Push) continue;

            var push = new PushOutcome(
                attack.Context.Attacker,
                attack.Context.Target,
                resolution.AttackResolution.IsHit,
                reciprocalPushes.Contains((attack.Context.Attacker.Id, attack.Context.Target.Id)));

            if (!push.IsHit) continue;

            // A mutual push pair needs both rolls before either unit moves (Total Warfare p. 148);
            // every other push displaces its units immediately after the attack.
            if (push.IsReciprocal)
            {
                deferredPushes.Add(push);
                continue;
            }

            _pushResolver.ApplyDisplacement(push.Attacker, push.Target);
        }

        _pushResolver.ApplyReciprocal(deferredPushes);

        _endOfPhasePsrCalculator.Calculate();
        Game.TransitionToNextPhase(Name);
    }

    public override void HandleCommand(IGameCommand command)
    {
        // This phase doesn't process incoming commands, but we need to implement this method
    }

    public override PhaseNames Name => PhaseNames.PhysicalAttackResolution;
}
