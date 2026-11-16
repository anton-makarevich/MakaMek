using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Resolves the physical attacks declared this turn, following the weapon attack resolution
/// pattern: a queue in resolution order with one item per declared limb (or one for a
/// paired-arm attack such as Push), one per charge and one per DFA, the pre-attack gates, the
/// resolver, then criticals, consciousness rolls and the end-of-phase piloting skill rolls.
/// </summary>
/// <remarks>
/// The queue is built once, before any damage of the phase, so an attack is never dropped because
/// an earlier one in the same phase destroyed or felled its attacker or its target. Attacks resolve
/// from the lower Initiative to the higher (Total Warfare p. 144): a charge or DFA declared during
/// the Movement Phase keeps its attacker's slot instead of always resolving last. Damage is applied
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
    private readonly PhysicalAttackDfaResolver _dfaResolver;
    private readonly PhysicalAttackPushResolver _pushResolver;
    private readonly PhysicalAttackEndOfPhasePsrCalculator _endOfPhasePsrCalculator;

    private List<ResolutionQueueItem> _queue = [];
    private int _cursor;
    private HashSet<(Guid AttackerId, Guid TargetId)> _reciprocalPushes = [];
    private List<PushOutcome> _deferredPushes = [];

    public PhysicalAttackResolutionPhase(ServerGame game,
        PushDisplacementResolver pushDisplacementResolver,
        ChargeDisplacementResolver chargeDisplacementResolver,
        DfaDisplacementResolver dfaDisplacementResolver,
        IDfaAttackResolver dfaAttackResolver) : base(game)
    {
        // The eligibility policy owns the attacker and target state as well as the reach rules, so
        // this gate is the only pre-attack check: it re-runs the same evaluation the declaration used.
        _gates = [new PhysicalAttackTargetValidityGate(game.PhysicalAttackEligibility)];

        var displacement = new PhysicalAttackDisplacementService(game);
        var consequences = new PhysicalAttackConsequenceApplier(game, _state);
        _queueBuilder = new PhysicalAttackResolutionQueueBuilder(game, _gates);
        _singleResolver = new PhysicalAttackSingleResolver(game, consequences);
        _chargeResolver = new PhysicalAttackChargeResolver(
            game, chargeDisplacementResolver, displacement, consequences);
        _dfaResolver = new PhysicalAttackDfaResolver(
            game, _state, dfaDisplacementResolver, dfaAttackResolver, displacement, consequences);
        _pushResolver = new PhysicalAttackPushResolver(
            game, _state, pushDisplacementResolver, displacement);
        _endOfPhasePsrCalculator = new PhysicalAttackEndOfPhasePsrCalculator(game, _state);
    }

    public override void Enter()
    {
        base.Enter();
        _state.Clear();
        _deferredPushes = [];
        _cursor = 0;

        // The queue is snapshotted before any damage: an attack must still resolve when an earlier
        // attack of the phase destroys its attacker or target, and Unit clears a destroyed unit's
        // pending declaration.
        _queue = _queueBuilder.Build();
        _reciprocalPushes = _queue.GetReciprocalPushPairs();

        ProcessQueue();
    }

    public override void HandleCommand(IGameCommand command)
    {
        // The phase is not driven by client commands: a DFA miss displacement resolves on the server.
    }

    /// <summary>
    /// Resolves the snapshot queue in order and finishes the phase when it is drained.
    /// </summary>
    private void ProcessQueue()
    {
        while (_cursor < _queue.Count)
            ProcessQueueItem(_queue[_cursor++]);

        Finish();
    }

    private void ProcessQueueItem(ResolutionQueueItem item)
    {
        if (item.Charge is { } charge)
        {
            _chargeResolver.Resolve(charge);
            return;
        }

        if (item.Dfa is { } dfa)
        {
            _dfaResolver.Resolve(dfa);
            return;
        }

        var attack = item.Attack!.Value;
        var resolution = _singleResolver.Resolve(attack);
        if (attack.Context.AttackType != PhysicalAttackType.Push) return;

        var push = new PushOutcome(
            attack.Context.Attacker,
            attack.Context.Target,
            resolution.AttackResolution.IsHit,
            _reciprocalPushes.Contains((attack.Context.Attacker.Id, attack.Context.Target.Id)));

        if (!push.IsHit) return;

        // A mutual push pair needs both rolls before either unit moves (Total Warfare p. 148);
        // every other push displaces its units immediately after the attack.
        if (push.IsReciprocal)
        {
            _deferredPushes.Add(push);
            return;
        }

        _pushResolver.ApplyDisplacement(push.Attacker, push.Target);
    }

    private void Finish()
    {
        _pushResolver.ApplyReciprocal(_deferredPushes);
        _endOfPhasePsrCalculator.Calculate();
        Game.TransitionToNextPhase(Name);
    }

    public override PhaseNames Name => PhaseNames.PhysicalAttackResolution;
}
