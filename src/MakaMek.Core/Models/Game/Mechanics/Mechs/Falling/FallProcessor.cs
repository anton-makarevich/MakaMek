using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Map;
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

    public IEnumerable<MechFallingCommand> ProcessPotentialFall(
        Unit unit,
        BattleMap? battleMap,
        List<ComponentHitData> componentHits,
        int totalDamage,
        Guid gameId,
        List<PartLocation>? destroyedPartLocations = null)
    {
        var commandsToReturn = new List<MechFallingCommand>();

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
                var gyro = unit.GetAllComponents<Gyro>().FirstOrDefault();
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
        if (totalDamage >= heavyDamageThreshold && unit is Mech) // Ensure unit is a Mech for heavy damage PSR
        {
            fallReasons.Add(FallReasonType.HeavyDamage);
        }
        
        // Check for destroyed legs
        if (destroyedPartLocations?.Any(location => location.IsLeg()) == true)
        {
            fallReasons.Add(FallReasonType.LegDestroyed);
        }

        if (fallReasons.Count == 0)
            return commandsToReturn;

        foreach (var reasonType in fallReasons)
        {
            var requiresPsr = reasonType.RequiresPilotingSkillRoll();
            var isFallingNow = !requiresPsr; // Auto-fall if PSR not required
            
            PilotingSkillRollData? fallPsrData = null;

            if (requiresPsr && reasonType.ToPilotingSkillRollType() is { } psrRollType)
            {
                var psrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(unit, [psrRollType],
                    battleMap, totalDamage);
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

            if (isFallingNow)
            {
                var pilotPsrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(
                    unit,
                    [PilotingSkillRollType.PilotDamageFromFall],
                    battleMap,
                    totalDamage);

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
                ? _fallingDamageCalculator.CalculateFallingDamage(unit, 0, false)
                : null;

            var command = new MechFallingCommand
            {
                UnitId = unit.Id,
                LevelsFallen = 0,
                WasJumping = false,
                DamageData = fallingDamageData,
                GameOriginId = gameId,
                FallPilotingSkillRoll = fallPsrData,
                PilotDamagePilotingSkillRoll = pilotDamagePsr
            };
            commandsToReturn.Add(command);
            if (isFallingNow) break;
        }

        return commandsToReturn;
    }
}