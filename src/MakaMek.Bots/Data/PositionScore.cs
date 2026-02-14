using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Represents the tactical score for a candidate movement position with a specific movement type
/// </summary>
public readonly record struct PositionScore
{
    /// <summary>
    /// The position being evaluated
    /// </summary>
    public required HexPosition Position { get; init; }

    /// <summary>
    /// The movement type used to reach this position
    /// </summary>
    public required MovementType MovementType { get; init; }
    
    /// <summary>
    /// The path to reach this position
    /// </summary>
    public required MovementPath Path { get; init; }

    /// <summary>
    /// Defensive threat index - lower is better (less vulnerable to enemy fire)
    /// </summary>
    public required double DefensiveIndex { get; init; }

    /// <summary>
    /// Offensive potential index - higher is better (more damage potential)
    /// </summary>
    public required double OffensiveIndex { get; init; }
    
    /// <summary>
    /// The number of enemy units that can fire into the rear arc
    /// </summary>
    public required int EnemiesInRearArc { get; init; }
}
