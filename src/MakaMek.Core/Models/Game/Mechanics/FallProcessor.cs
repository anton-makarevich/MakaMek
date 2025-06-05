using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

public class FallProcessor : IFallProcessor
{
    private readonly IRulesProvider _rulesProvider;
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator;
    private readonly IDiceRoller _diceRoller;
    private readonly IFallingDamageCalculator _fallingDamageCalculator;

    private static readonly Dictionary<MakaMekComponent, PilotingSkillRollType> FallInducingCriticalsMap = new()
    {
        { MakaMekComponent.Gyro, PilotingSkillRollType.GyroHit },
        { MakaMekComponent.LowerLegActuator, PilotingSkillRollType.LowerLegActuatorHit }
    };

    private record FallReason(PilotingSkillRollType RollType, MakaMekComponent? ComponentType = null);

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
        Guid gameId)
    {
        var commandsToReturn = new List<MechFallingCommand>();

        var hitFallInducingComponentTypes = componentHits
            .Select(c => c.Type)
            .Where(type => FallInducingCriticalsMap.ContainsKey(type))
            .ToList();

        var fallReasons = hitFallInducingComponentTypes
            .Select(componentType =>
                new FallReason(FallInducingCriticalsMap[componentType], componentType)).ToList();

        var heavyDamageThreshold = _rulesProvider.GetHeavyDamageThreshold();
        if (totalDamage >= heavyDamageThreshold && unit is Mech) // Ensure unit is a Mech for heavy damage PSR
        {
            fallReasons.Add(new FallReason(PilotingSkillRollType.HeavyDamage));
        }

        if (fallReasons.Count == 0)
            return commandsToReturn;

        foreach (var reason in fallReasons)
        {
            var autoFall = false;
            var requiresPsr = true;

            if (reason.ComponentType == MakaMekComponent.Gyro)
            {
                var gyro = unit.GetAllComponents<Gyro>().FirstOrDefault();
                if (gyro == null) continue;

                if (gyro.IsDestroyed)
                {
                    autoFall = true;
                    requiresPsr = false;
                }
            }

            PilotingSkillRollData? fallPsrData = null;
            var isFallingNow = autoFall;

            if (requiresPsr && !autoFall)
            {
                var psrBreakdown = _pilotingSkillCalculator.GetPsrBreakdown(unit, [reason.RollType],
                    battleMap, totalDamage);
                var diceResults = _diceRoller.Roll2D6();
                var rollTotal = diceResults.Sum(d => d.Result);
                isFallingNow = rollTotal < psrBreakdown.ModifiedPilotingSkill;

                fallPsrData = new PilotingSkillRollData
                {
                    RollType = reason.RollType,
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
                    battleMap);

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