using System.Collections.Frozen;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

// Pre-existing class coupling in this type, not introduced by this change. It is tracked in
// #1714 together with the baseline-aware analyzer gate that will make this suppression unnecessary.
#pragma warning disable CA1506

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

public class FallProcessor : IFallProcessor
{
    private readonly IRulesProvider _rulesProvider;
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator;
    private readonly IFallingDamageCalculator _fallingDamageCalculator;

    /// <summary>
    /// Maps critical-hit component types to the PSR roll type they trigger.
    /// </summary>
    private static readonly FrozenDictionary<MakaMekComponent, PilotingSkillRollType> ComponentFallReasonMap =
        new Dictionary<MakaMekComponent, PilotingSkillRollType>
        {
            { MakaMekComponent.Gyro, PilotingSkillRollType.GyroHit },
            { MakaMekComponent.LowerLegActuator, PilotingSkillRollType.LowerLegActuatorHit },
            { MakaMekComponent.UpperLegActuator, PilotingSkillRollType.UpperLegActuatorHit },
            { MakaMekComponent.Hip, PilotingSkillRollType.HipActuatorHit },
            { MakaMekComponent.FootActuator, PilotingSkillRollType.FootActuatorHit }
        }.ToFrozenDictionary();

    public FallProcessor(
        IRulesProvider rulesProvider,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IFallingDamageCalculator fallingDamageCalculator)
    {
        _rulesProvider = rulesProvider;
        _pilotingSkillCalculator = pilotingSkillCalculator;
        _fallingDamageCalculator = fallingDamageCalculator;
    }

    public IEnumerable<MechFallCommand> ProcessPotentialFall(
        Mech mech,
        IGame game,
        List<ComponentHitData> componentHits,
        List<PartLocation>? destroyedPartLocations = null,
        IReadOnlyList<PilotingSkillRollContext>? additionalRollContexts = null)
    {
        if (mech.IsProne) return []; // Prone mechs cannot fall

        var rollContexts = new List<PilotingSkillRollContext>();

        // Check for component critical hits that may cause falling
        var hitFallInducingComponentTypes = componentHits
            .Select(c => c.Type)
            .Where(type => ComponentFallReasonMap.ContainsKey(type))
            .ToList();

        // Build contexts from critical hits
        foreach (var componentType in hitFallInducingComponentTypes)
        {
            var rollType = ComponentFallReasonMap[componentType];

            // Special handling for gyro - auto-fall if the gyro is completely destroyed
            if (componentType == MakaMekComponent.Gyro)
            {
                var gyro = mech.GetAllComponents<Gyro>().FirstOrDefault();
                if (gyro == null) continue;
                if (gyro.IsDestroyed)
                    rollType = PilotingSkillRollType.GyroDestroyed;
            }

            rollContexts.Add(new PilotingSkillRollContext(rollType));
        }

        // Check for heavy damage
        var heavyDamageThreshold = _rulesProvider.GetHeavyDamageThreshold();
        if (mech.TotalPhaseDamage >= heavyDamageThreshold)
            rollContexts.Add(new PilotingSkillRollContext(PilotingSkillRollType.HeavyDamage));

        // Check for destroyed legs (automatic fall)
        if (destroyedPartLocations?.Any(location => location.IsLeg()) == true)
            rollContexts.Add(new PilotingSkillRollContext(PilotingSkillRollType.LegDestroyed));

        if (additionalRollContexts != null)
            rollContexts.AddRange(additionalRollContexts);

        return ProcessRollContexts(rollContexts, mech, game)
            .Select(f => f.ToMechFallCommand());
    }

    public FallContextData ProcessMovementAttempt(
        Mech mech,
        PilotingSkillRollContext rollContext,
        IGame game,
        MovementType movementType,
        HexPosition? fallPosition = null)
        => ProcessRollContexts([rollContext], mech, game, movementType, fallPosition).First();

    /// <summary>
    /// Core processing loop: evaluates each roll context in order (auto-falls first),
    /// rolls PSRs where required, and stops as soon as the mech begins falling.
    /// </summary>
    private List<FallContextData> ProcessRollContexts(
        List<PilotingSkillRollContext> rollContexts,
        Mech mech,
        IGame game,
        MovementType movementType = MovementType.StandingStill,
        HexPosition? fallPosition = null)
    {
        if (rollContexts.Count == 0)
            return [];

        var results = new List<FallContextData>();

        // Process automatic falls first (GyroDestroyed, LegDestroyed) as they take
        // precedence over PSR-required falls (heavy damage, actuator hits, etc.)
        var sortedContexts = rollContexts
            .OrderBy(ctx => _rulesProvider.RequiresPilotingSkillRoll(ctx.RollType))
            .ToList();

        foreach (var context in sortedContexts)
        {
            var (isFallingNow, fallPsrData) = EvaluateFall(mech, context, game);

            // Water depth of the hex where the fall happens (0 when the hex is dry or the surface is not Ground).
            // For water entry the roll context carries the authoritative depth (it is built from the actual hex).
            var waterDepth = context is EnteringDeepWaterRollContext deepWaterCtx
                ? deepWaterCtx.WaterDepth
                : ResolveWaterDepth(mech, game, fallPosition);

            // Determine levels fallen based on context and movement type
            var levelsFallen = 0;
            var wasJumping = movementType == MovementType.Jump;
            PilotingSkillRollData? pilotDamagePsr = null;
            FallingDamageData? fallingDamageData = null;
            FallingDamageData? skidDamageData = null;

            if (isFallingNow)
            {
                (levelsFallen, wasJumping) = ResolveFallParameters(context, movementType, waterDepth);
                pilotDamagePsr = EvaluatePilotDamage(mech, levelsFallen, game);
                fallingDamageData = GetFallingDamage(mech, context, levelsFallen, waterDepth);

                if (context is SkidCheckRollContext skidCtx && skidCtx.SkidDistance > 0)
                {
                    skidDamageData = _fallingDamageCalculator.CalculateSkidDamage(
                        mech,
                        skidCtx.SkidDistance,
                        fallingDamageData.FacingAfterFall,
                        fallingDamageData.FacingDiceRoll,
                        fallingDamageData.FallDirection);
                }
            }

            var fallContextData = new FallContextData
            {
                UnitId = mech.Id,
                GameId = game.Id,
                IsFalling = isFallingNow,
                PilotingSkillRoll = fallPsrData,
                PilotDamagePilotingSkillRoll = pilotDamagePsr,
                FallingDamageData = fallingDamageData,
                LevelsFallen = levelsFallen,
                WasJumping = wasJumping,
                SkidDamageData = skidDamageData,
                SkidDistance = context is SkidCheckRollContext skidCtx2 ? skidCtx2.SkidDistance : 0
            };

            results.Add(fallContextData);

            if (isFallingNow) break;
        }

        return results;
    }

    /// <summary>
    /// Whether the context makes the mech fall now, and the roll that decided it. An automatic fall
    /// (no PSR required) reports no roll; a required roll that fails makes the mech fall.
    /// </summary>
    private (bool IsFalling, PilotingSkillRollData? Psr) EvaluateFall(
        Mech mech,
        PilotingSkillRollContext context,
        IGame game)
    {
        if (!_rulesProvider.RequiresPilotingSkillRoll(context.RollType))
            return (true, null);

        var psrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(mech, context, game);
        var psr = _pilotingSkillCalculator.EvaluateRoll(psrBreakdown, mech, context);
        return (!psr.IsSuccessful, psr);
    }

    /// <summary>
    /// The pilot damage roll a fall triggers, based on how far the mech fell.
    /// </summary>
    private PilotingSkillRollData EvaluatePilotDamage(Mech mech, int levelsFallen, IGame game)
    {
        var context = new PilotDamageFromFallRollContext(levelsFallen);
        var psrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(mech, context, game);
        return _pilotingSkillCalculator.EvaluateRoll(psrBreakdown, mech, context);
    }

    /// <summary>
    /// How far the unit falls and whether the fall is reported as happening mid-jump. Each roll
    /// context carries its own drop; only a failed jump landing falls back to the water depth, since
    /// the unit drops from the surface to the hex bottom.
    /// </summary>
    private static (int LevelsFallen, bool WasJumping) ResolveFallParameters(
        PilotingSkillRollContext context,
        MovementType movementType,
        int waterDepth)
    {
        var wasJumping = movementType == MovementType.Jump;

        return context switch
        {
            // WaterTerrain.Height stores depth as negative integers (0 = shallow, -1 = depth 1, -2 = depth 2).
            // EnteringDeepWaterRollContext.WaterDepth is already the positive depth value (-1 * water.Height).
            // A fall that ends in a water hex drops the unit to the hex bottom, so for a water-entry
            // fall levelsFallen equals the water depth, regardless of the movement type
            // (walk, run or jump all fall the same levels).
            EnteringDeepWaterRollContext waterCtx => (waterCtx.WaterDepth, wasJumping),
            // A collapsing bridge drops the unit from the bridge deck down to the hex bottom
            BridgeCollapseRollContext bridgeCtx => (bridgeCtx.BridgeHeight + waterDepth, wasJumping),
            // A skid is a fall in place on the same level.
            SkidCheckRollContext => (0, false),
            CliffFallRollContext cliffCtx => (cliffCtx.LevelsFallen, false),
            // The DFA attacker is jumping, so WasJumping stays true and the fall is reported as
            // happening mid-jump. The jump-into-water fallback below is skipped because a DFA fall
            // is a fixed level drop, not a landing at the hex bottom.
            DfaFallRollContext dfaCtx => (dfaCtx.LevelsFallen, wasJumping),
            // A failed jump landing in a water hex drops the unit from the surface to the hex bottom
            _ when movementType == MovementType.Jump && waterDepth > 0 => (waterDepth, wasJumping),
            _ => (0, wasJumping)
        };
    }

    /// <summary>
    /// Falling damage for the context: a cliff or DFA fall uses its own fixed facing roll (the DFA
    /// roll makes every hit location rear), every other fall rolls the facing.
    /// </summary>
    private FallingDamageData GetFallingDamage(
        Mech mech,
        PilotingSkillRollContext context,
        int levelsFallen,
        int waterDepth) =>
        context switch
        {
            CliffFallRollContext cliffCtx => _fallingDamageCalculator.CalculateFallingDamage(
                mech, levelsFallen, waterDepth, cliffCtx.FacingDiceRoll, cliffCtx.FacingAfterFall),
            DfaFallRollContext dfaCtx => _fallingDamageCalculator.CalculateFallingDamage(
                mech, levelsFallen, waterDepth, dfaCtx.FacingDiceRoll, dfaCtx.FacingAfterFall),
            _ => _fallingDamageCalculator.CalculateFallingDamage(mech, levelsFallen, waterDepth)
        };

    /// <summary>
    /// Resolves the water depth of the hex where the fall happens.
    /// Uses the explicit landing position when supplied (movement interrupts calculate the fall
    /// before the unit moves), otherwise the mech's current position.
    /// Depth only counts on a Ground surface: a unit on a bridge falls onto the deck, not into the water.
    /// Returns 0 when the hex cannot be resolved or has no water.
    /// </summary>
    private static int ResolveWaterDepth(Mech mech, IGame game, HexPosition? fallPosition)
    {
        var position = fallPosition ?? mech.Position;
        if (position == null) return 0;
        if (position.Surface != HexSurface.Ground) return 0;

        var hex = game.BattleMap?.GetHex(position.Coordinates);
        return hex?.GetWaterDepth() ?? 0;
    }
}