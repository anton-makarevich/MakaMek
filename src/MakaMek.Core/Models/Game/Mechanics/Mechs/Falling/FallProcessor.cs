using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

public class FallProcessor : IFallProcessor
{
    private readonly IRulesProvider _rulesProvider;
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator;
    private readonly IDiceRoller _diceRoller;
    private readonly IFallingDamageCalculator _fallingDamageCalculator;

    private static readonly Dictionary<MakaMekComponent, FallReasonType> ComponentFallReasonMap = new()
    {
        { MakaMekComponent.Gyro, FallReasonType.GyroHit },
        { MakaMekComponent.LowerLegActuator, FallReasonType.LowerLegActuatorHit }
    };

    public FallProcessor(
        IRulesProvider rulesProvider,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IDiceRoller diceRoller,
        IFallingDamageCalculator fallingDamageCalculator)
    {
        _rulesProvider = rulesProvider;
        _pilotingSkillCalculator = pilotingSkillCalculator;
        _diceRoller = diceRoller;
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

    public FallContextData ProcessStandupAttempt(Mech mech, IGame game)
    {
        return GetFallContextForReasons([FallReasonType.StandUpAttempt], mech, game).First();
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
                var psrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(mech, psrRollType,
                    game);
                var diceResults = _diceRoller.Roll2D6();
                var rollTotal = diceResults.Sum(d => d.Result);
                isFallingNow = rollTotal < psrBreakdown.ModifiedPilotingSkill;

                fallPsrData = new PilotingSkillRollData
                {
                    RollType = psrRollType,
                    DiceResults = diceResults.Select(d => d.Result).ToArray(),
                    IsSuccessful = !isFallingNow,
                    PsrBreakdown = psrBreakdown
                };
            }

            PilotingSkillRollData? pilotDamagePsr = null;

            if (isFallingNow && reasonType != FallReasonType.StandUpAttempt)
            {
                var pilotPsrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(
                    mech,
                    PilotingSkillRollType.PilotDamageFromFall,
                    game);

                if (pilotPsrBreakdown.Modifiers.Any())
                {
                    var pilotDiceResults = _diceRoller.Roll2D6();
                    var pilotRollTotal = pilotDiceResults.Sum(d => d.Result);
                    var isPilotDamageSuccessful = pilotRollTotal >= pilotPsrBreakdown.ModifiedPilotingSkill;

                    pilotDamagePsr = new PilotingSkillRollData
                    {
                        RollType = PilotingSkillRollType.PilotDamageFromFall,
                        DiceResults = pilotDiceResults.Select(d => d.Result).ToArray(),
                        IsSuccessful = isPilotDamageSuccessful,
                        PsrBreakdown = pilotPsrBreakdown
                    };
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