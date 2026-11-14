using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// One attack of the phase in resolution order: either a limb-based attack or a charge.
/// </summary>
internal sealed record ResolutionQueueItem(AttackQueueItem? Attack, ChargeQueueItem? Charge);

internal readonly record struct AttackQueueItem(IPlayer Player, PhysicalAttackContext Context);

internal sealed record ChargeQueueItem(
    IPlayer Player,
    IUnit Attacker,
    Guid DeclaredTargetId,
    int HexesMoved,
    bool AttackerAutoFail);

/// <summary>
/// The outcome of one push attack, used to pair mutual pushes before displacing them.
/// </summary>
internal sealed record PushOutcome(IUnit Attacker, IUnit Target, bool IsHit, bool IsReciprocal);

/// <summary>
/// What a unit took during the phase, for the piloting skill rolls at its end.
/// </summary>
internal sealed class UnitPhaseAccumulatedDamage
{
    public List<ComponentHitData> ComponentHits { get; } = [];
    public HashSet<PartLocation> DestroyedParts { get; } = [];
    public List<PilotingSkillRollContext> RollContexts { get; } = [];
}

/// <summary>
/// Mutable per-phase state shared by the physical-attack resolution collaborators: the damage
/// accumulated per unit and the push PSRs that are waiting for their displacement.
/// </summary>
internal sealed class PhysicalAttackPhaseState
{
    public Dictionary<Guid, UnitPhaseAccumulatedDamage> AccumulatedDamage { get; } = new();

    public Dictionary<Guid, (List<PilotingSkillRollContext> Contexts, PushHitRollContext Context)> PushHitContexts
    {
        get;
    } = new();

    public void Clear()
    {
        AccumulatedDamage.Clear();
        PushHitContexts.Clear();
    }

    public UnitPhaseAccumulatedDamage GetAccumulatedDamage(IUnit unit)
    {
        if (AccumulatedDamage.TryGetValue(unit.Id, out var accumulated)) return accumulated;
        accumulated = new UnitPhaseAccumulatedDamage();
        AccumulatedDamage[unit.Id] = accumulated;
        return accumulated;
    }
}
