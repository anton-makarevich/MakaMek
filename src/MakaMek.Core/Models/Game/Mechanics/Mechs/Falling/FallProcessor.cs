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
    private IEnumerable<FallContextData> ProcessRollContexts(
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
            var requiresPsr = _rulesProvider.RequiresPilotingSkillRoll(context.RollType);
            var isFallingNow = !requiresPsr; // Auto-fall when no PSR is required

            PilotingSkillRollData? fallPsrData = null;

            if (requiresPsr)
            {
                var psrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(mech, context, game);
                fallPsrData = _pilotingSkillCalculator.EvaluateRoll(psrBreakdown, mech, context);
                isFallingNow = !fallPsrData.IsSuccessful;
            }

            // Water depth of the hex where the fall happens (0 when the hex is dry or the surface is not Ground).
            // For water entry the roll context carries the authoritative depth (it is built from the actual hex).
            var waterDepth = context is EnteringDeepWaterRollContext deepWaterCtx
                ? deepWaterCtx.WaterDepth
                : ResolveWaterDepth(mech, game, fallPosition);

            PilotingSkillRollData? pilotDamagePsr = null;

            // Determine levels fallen based on context and movement type
            var levelsFallen = 0;
            var wasJumping = movementType == MovementType.Jump;

            if (isFallingNow)
            {
                // WaterTerrain.Height stores depth as negative integers (0 = shallow, -1 = depth 1, -2 = depth 2).
                // EnteringDeepWaterRollContext.WaterDepth is already the positive depth value (-1 * water.Height).
                // A fall that ends in a water hex drops the unit to the hex bottom, so for a water-entry
                // fall levelsFallen equals the water depth, regardless of the movement type
                // (walk, run or jump all fall the same levels).
                if (context is EnteringDeepWaterRollContext waterCtx)
                {
                    levelsFallen = waterCtx.WaterDepth;
                }
                else if (context is BridgeCollapseRollContext bridgeCtx)
                {
                    // A collapsing bridge drops the unit from the bridge deck down to the hex bottom
                    levelsFallen = bridgeCtx.BridgeHeight + waterDepth;
                }
                else if (context is SkidCheckRollContext)
                {
                    levelsFallen = 0;
                    wasJumping = false;
                }
                else if (context is CliffFallRollContext cliffCtx)
                {
                    levelsFallen = cliffCtx.LevelsFallen;
                    wasJumping = false;
                }
                else if (movementType == MovementType.Jump && waterDepth > 0)
                {
                    // A failed jump landing in a water hex drops the unit from the surface
                    // to the hex bottom
                    levelsFallen = waterDepth;
                }

                var pilotDamageContext = new PilotDamageFromFallRollContext(levelsFallen);
                var pilotPsrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(mech, pilotDamageContext, game);

                pilotDamagePsr = _pilotingSkillCalculator.EvaluateRoll(pilotPsrBreakdown, mech, pilotDamageContext);
            }

            FallingDamageData? fallingDamageData = null;
            FallingDamageData? skidDamageData = null;

            if (isFallingNow)
            {
                if (context is CliffFallRollContext cliffCtx2)
                {
                    fallingDamageData = _fallingDamageCalculator.CalculateFallingDamage(
                        mech, levelsFallen, waterDepth,
                        cliffCtx2.FacingDiceRoll, cliffCtx2.FacingAfterFall);
                }
                else
                {
                    fallingDamageData = _fallingDamageCalculator.CalculateFallingDamage(
                        mech, levelsFallen, waterDepth);
                }

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
    /// Resolves the water depth of the hex where the fall happens.
    /// Uses the explicit landing position when supplied (movement interrupts calculate the fall
    /// before the unit moves), otherwise the mech's current position.
    /// Depth only counts on a Ground surface: a unit on a bridge falls onto the deck, not into the water.
    /// Returns 0 when the hex cannot be resolved or has no water.
    /// </summary>
    private int ResolveWaterDepth(Mech mech, IGame game, HexPosition? fallPosition)
    {
        var position = fallPosition ?? mech.Position;
        if (position == null) return 0;
        if (position.Surface != HexSurface.Ground) return 0;

        var hex = game.BattleMap?.GetHex(position.Coordinates);
        return hex?.GetWaterDepth() ?? 0;
    }
}