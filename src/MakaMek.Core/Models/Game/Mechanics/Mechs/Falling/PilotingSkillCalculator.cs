using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

/// <summary>
/// Classic BattleTech implementation of piloting skill roll calculator
/// </summary>
public class PilotingSkillCalculator : IPilotingSkillCalculator
{
    private readonly IDiceRoller _diceRoller;
    private readonly PilotingSkillModifierCalculator _modifierCalculator;

    public PilotingSkillCalculator(
        IRulesProvider rules,
        IDiceRoller diceRoller,
        PilotingSkillModifierCalculator? modifierCalculator = null)
    {
        _diceRoller = diceRoller;
        _modifierCalculator = modifierCalculator ?? new PilotingSkillModifierCalculator(rules);
    }
    
    /// <summary>
    /// Gets a detailed breakdown of all modifiers affecting the piloting skill roll.
    /// The context carries the roll type and any extra data (e.g. water depth, levels fallen).
    /// </summary>
    /// <param name="unit">The unit making the piloting skill roll</param>
    /// <param name="context">The piloting skill roll context</param>
    /// <param name="game">The game instance, used for accessing the map and other game state</param>
    /// <returns>A breakdown of the piloting skill roll calculation</returns>
    public PsrBreakdown GetPsrBreakdown(
        Unit unit,
        PilotingSkillRollContext context,
        IGame? game = null)
        => BuildPsrBreakdown(unit, context);

    private PsrBreakdown BuildPsrBreakdown(Unit unit, PilotingSkillRollContext context)
    {
        if (unit.Pilot == null)
        {
            throw new ArgumentException("Unit has no pilot", nameof(unit));
        }

        if (unit is not Mech mech)
            return new PsrBreakdown
            {
                BasePilotingSkill = unit.Pilot.Piloting,
                Modifiers = new List<RollModifier>()
            };

        var modifiers = new List<RollModifier>();

        // Add standard modifiers
        modifiers.AddRange(_modifierCalculator.GetStandardModifiers(mech));

        // Add special modifiers derived from the context
        modifiers.AddRange(_modifierCalculator.GetContextModifiers(context));

        return new PsrBreakdown
        {
            BasePilotingSkill = unit.Pilot.Piloting,
            Modifiers = modifiers
        };
    }

    /// <summary>
    /// Evaluates a piloting skill roll and returns complete roll data
    /// </summary>
    /// <param name="psrBreakdown">The PSR breakdown containing target number and modifiers</param>
    /// <param name="unit">The unit making the piloting skill roll</param>
    /// <param name="context">The piloting skill roll context</param>
    /// <returns>Complete piloting skill roll data including dice results</returns>
    public PilotingSkillRollData EvaluateRoll(PsrBreakdown psrBreakdown, Unit unit, PilotingSkillRollContext context)
    {
        if (unit.Pilot == null)
        {
            throw new ArgumentException("Unit has no pilot", nameof(unit));
        }

        // For unconscious pilots, create automatic failure data without rolling dice
        if (!unit.Pilot.IsConscious)
        {
            return new PilotingSkillRollData
            {
                RollContext = context,
                DiceResults = [],
                IsSuccessful = false,
                PsrBreakdown = psrBreakdown
            };
        }

        // Perform 2d6 dice roll
        var diceResults = _diceRoller.Roll2D6();
        var rollTotal = diceResults.Sum(d => d.Result);
        var isSuccessful = rollTotal >= psrBreakdown.ModifiedPilotingSkill;

        return new PilotingSkillRollData
        {
            RollContext = context,
            DiceResults = diceResults.Select(d => d.Result).ToArray(),
            IsSuccessful = isSuccessful,
            PsrBreakdown = psrBreakdown
        };
    }

}
