namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Represents the defensive evaluation result for a movement path, including both the defensive threat score and the count of enemies positioned in the unit's rear arc.
/// </summary>
/// <param name="Score">The defensive threat index - lower values indicate safer positions.</param>
/// <param name="EnemiesInRearArc">The number of enemy units that can fire into the rear arc.</param>
public record struct PathDefensiveScore(double Score, int EnemiesInRearArc);