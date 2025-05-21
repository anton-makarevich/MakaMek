using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Holds the breakdown of a piloting skill roll, including all modifiers
/// </summary>
public record PsrBreakdown
{
    /// <summary>
    /// Base piloting skill value
    /// </summary>
    public required int BasePilotingSkill { get; init; }
    
    /// <summary>
    /// List of all modifiers affecting the piloting skill roll
    /// </summary>
    public required IReadOnlyList<RollModifier> Modifiers { get; init; }
    
    /// <summary>
    /// The impossible roll value
    /// </summary>
    public const int ImpossibleRoll = 13;
    
    /// <summary>
    /// Gets the total target number for the piloting skill roll
    /// </summary>
    public int ModifiedPilotingSkill => BasePilotingSkill + Modifiers.Sum(m => m.Value);
    
    /// <summary>
    /// Determines if the roll is impossible (greater than 12 on 2d6)
    /// </summary>
    public bool IsImpossible => ModifiedPilotingSkill >= ImpossibleRoll;
}
