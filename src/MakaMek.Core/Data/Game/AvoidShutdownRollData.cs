namespace Sanet.MakaMek.Core.Data.Game;

public record AvoidShutdownRollData
{
    /// <summary>
    /// The heat level that triggered the shutdown 
    /// </summary>
    public int HeatLevel { get; init; }

    /// <summary>
    /// The dice results for the shutdown avoidance roll 
    /// </summary>
    public int[] DiceResults { get; init; } = [];
    
    /// <summary>
    /// The target number that needed to be met to avoid shutdown (if applicable)
    /// </summary>
    public int AvoidNumber { get; init; }
    
    /// <summary>
    /// Whether the roll was successful
    /// </summary>
    public required bool IsSuccessful { get; init; }
}