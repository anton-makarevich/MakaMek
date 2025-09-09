using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units;
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

    private static readonly Dictionary<MakaMekComponent, FallReasonType> ComponentFallReasonMap = new()
    {
        { MakaMekComponent.Gyro, FallReasonType.GyroHit },
        { MakaMekComponent.LowerLegActuator, FallReasonType.LowerLegActuatorHit },
        { MakaMekComponent.UpperLegActuator, FallReasonType.UpperLegActuatorHit },
        { MakaMekComponent.Hip, FallReasonType.HipActuatorHit },
        { MakaMekComponent.FootActuator, FallReasonType.FootActuatorHit }
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
        var fallReasons = new List<FallReasonType>();

        // Check for component critical hits that may cause falling
        var hitFallInducingComponentTypes = componentHits
            .Select(c => c.Type)
            .Where(type => ComponentFallReasonMap.ContainsKey(type))
            .ToList();

        // Add reasons from critical hits
        foreach (var componentType in hitFallInducingComponentTypes)
        {
            var reasonType = ComponentFallReasonMap[componentType];
            
            // Special handling for gyro - check if it's destroyed
            if (componentType == MakaMekComponent.Gyro)
            {
                var gyro = mech.GetAllComponents<Gyro>().FirstOrDefault();
                if (gyro == null) continue;
                if (gyro.IsDestroyed)
                {
                    // If gyro is destroyed, change reason type to automatic fall
                    reasonType = FallReasonType.GyroDestroyed;
                }
            }
            
            fallReasons.Add(reasonType);
        }

        // Check for heavy damage
        var heavyDamageThreshold = _rulesProvider.GetHeavyDamageThreshold();
        if (mech.TotalPhaseDamage >= heavyDamageThreshold)
        {
            fallReasons.Add(FallReasonType.HeavyDamage);
        }
        
        // Check for destroyed legs
        if (destroyedPartLocations?.Any(location => location.IsLeg()) == true)
        {
            fallReasons.Add(FallReasonType.LegDestroyed);
        }

        return GetFallContextForReasons(fallReasons, mech, game)
            .Select(f => f.ToMechFallCommand());
    }

    public FallContextData ProcessMovementAttempt(Mech mech, FallReasonType possibleFallReason, IGame game)
    {
        return GetFallContextForReasons([possibleFallReason], mech, game).First();
    }
    
    private IEnumerable<FallContextData> GetFallContextForReasons(
        List<FallReasonType> fallReasons,
        Mech mech, 
        IGame game)
    {
        if (fallReasons.Count == 0)
            return [];

        var results = new List<FallContextData>();
        
        foreach (var reasonType in fallReasons)
        {
            var requiresPsr = reasonType.RequiresPilotingSkillRoll();
            var isFallingNow = !requiresPsr; // Auto-fall if PSR not required
            
            PilotingSkillRollData? fallPsrData = null;

            if (requiresPsr && reasonType.ToPilotingSkillRollType() is { } psrRollType)
            {
                var psrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(mech, psrRollType, game);
                fallPsrData = _pilotingSkillCalculator.EvaluateRoll(psrBreakdown, mech, psrRollType);
                isFallingNow = !fallPsrData.IsSuccessful;
            }

            PilotingSkillRollData? pilotDamagePsr = null;

            if (isFallingNow)
            {
                var pilotPsrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(
                    mech,
                    PilotingSkillRollType.PilotDamageFromFall,
                    game);

                if (pilotPsrBreakdown.Modifiers.Any())
                {
                    pilotDamagePsr = _pilotingSkillCalculator.EvaluateRoll(
                        pilotPsrBreakdown,
                        mech,
                        PilotingSkillRollType.PilotDamageFromFall);
                }
            }
            
            var fallingDamageData = isFallingNow
                ? _fallingDamageCalculator.CalculateFallingDamage(mech, 0, false)
                : null;

            var fallContextData = new FallContextData
            {
                UnitId = mech.Id,
                GameId = game.Id,
                IsFalling = isFallingNow,
                ReasonType = reasonType,
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