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
    private readonly FallParametersResolver _fallParametersResolver;
    private readonly FallingDamageResolver _fallingDamageResolver;

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
        FallParametersResolver fallParametersResolver,
        FallingDamageResolver fallingDamageResolver)
    {
        _rulesProvider = rulesProvider;
        _pilotingSkillCalculator = pilotingSkillCalculator;
        _fallParametersResolver = fallParametersResolver;
        _fallingDamageResolver = fallingDamageResolver;
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

            var parameters = _fallParametersResolver.Resolve(
                mech, game, context, movementType, isFallingNow, fallPosition);

            var pilotDamagePsr = isFallingNow
                ? EvaluatePilotDamage(mech, parameters.LevelsFallen, game)
                : null;

            var damage = _fallingDamageResolver.Resolve(
                mech, context, isFallingNow, parameters.LevelsFallen, parameters.WaterDepth);

            var fallContextData = new FallContextData
            {
                UnitId = mech.Id,
                GameId = game.Id,
                IsFalling = isFallingNow,
                PilotingSkillRoll = fallPsrData,
                PilotDamagePilotingSkillRoll = pilotDamagePsr,
                FallingDamageData = damage.FallingDamage,
                LevelsFallen = parameters.LevelsFallen,
                WasJumping = parameters.WasJumping,
                SkidDamageData = damage.SkidDamage,
                SkidDistance = context is SkidCheckRollContext skidCtx ? skidCtx.SkidDistance : 0
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
}
