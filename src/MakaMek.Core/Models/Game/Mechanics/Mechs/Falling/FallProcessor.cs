using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

public class FallProcessor : IFallProcessor
{
    private readonly IRulesProvider _rulesProvider;
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator;
    private readonly IFallingDamageCalculator _fallingDamageCalculator;

    /// <summary>
    /// Maps critical-hit component types to the PSR roll type they trigger.
    /// </summary>
    private static readonly Dictionary<MakaMekComponent, PilotingSkillRollType> ComponentFallReasonMap = new()
    {
        { MakaMekComponent.Gyro, PilotingSkillRollType.GyroHit },
        { MakaMekComponent.LowerLegActuator, PilotingSkillRollType.LowerLegActuatorHit },
        { MakaMekComponent.UpperLegActuator, PilotingSkillRollType.UpperLegActuatorHit },
        { MakaMekComponent.Hip, PilotingSkillRollType.HipActuatorHit },
        { MakaMekComponent.FootActuator, PilotingSkillRollType.FootActuatorHit }
    };

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
        List<PartLocation>? destroyedPartLocations = null)
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

        return ProcessRollContexts(rollContexts, mech, game)
            .Select(f => f.ToMechFallCommand());
    }

    public FallContextData ProcessMovementAttempt(Mech mech, PilotingSkillRollContext rollContext, IGame game)
        => ProcessRollContexts([rollContext], mech, game).First();

    /// <summary>
    /// Core processing loop: evaluates each roll context in order (auto-falls first),
    /// rolls PSRs where required, and stops as soon as the mech begins falling.
    /// </summary>
    private IEnumerable<FallContextData> ProcessRollContexts(
        List<PilotingSkillRollContext> rollContexts,
        Mech mech,
        IGame game)
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

            PilotingSkillRollData? pilotDamagePsr = null;

            if (isFallingNow)
            {
                var pilotDamageContext = new PilotDamageFromFallRollContext(LevelsFallen: 0);
                var pilotPsrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(mech, pilotDamageContext, game);

                if (pilotPsrBreakdown.Modifiers.Any())
                    pilotDamagePsr = _pilotingSkillCalculator.EvaluateRoll(pilotPsrBreakdown, mech, pilotDamageContext);
            }

            var fallingDamageData = isFallingNow
                ? _fallingDamageCalculator.CalculateFallingDamage(mech, 0, false)
                : null;

            var fallContextData = new FallContextData
            {
                UnitId = mech.Id,
                GameId = game.Id,
                IsFalling = isFallingNow,
                PilotingSkillRoll = fallPsrData,
                PilotDamagePilotingSkillRoll = pilotDamagePsr,
                FallingDamageData = fallingDamageData,
                LevelsFallen = 0,
                WasJumping = false
            };

            results.Add(fallContextData);

            if (isFallingNow) break;
        }

        return results;
    }
}