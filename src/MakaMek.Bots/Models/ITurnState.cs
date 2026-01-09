using Sanet.MakaMek.Bots.Data;

namespace Sanet.MakaMek.Bots.Models;

/// <summary>
/// Interface for managing turn-specific cached data for bot decision making
/// </summary>
public interface ITurnState
{
    /// <summary>
    /// The game ID this turn state is associated with
    /// </summary>
    Guid GameId { get; }
    
    /// <summary>
    /// The turn number this state is for
    /// </summary>
    int TurnNumber { get; }

    /// <summary>
    /// The ID of the unit currently active in the phase (e.g. continuing action).
    /// If null, any unit can be selected.
    /// </summary>
    Guid? PhaseActiveUnitId { get; set; }
    
    /// <summary>
    /// Attempts to get cached target evaluation data for a specific attacker-target pair
    /// </summary>
    /// <param name="key">The cache key for the attacker-target pair</param>
    /// <param name="data">The cached evaluation data if found</param>
    /// <returns>True if cached data was found, false otherwise</returns>
    bool TryGetTargetEvaluation(TargetEvaluationKey key, out TargetEvaluationData data);
    
    /// <summary>
    /// Adds target evaluation data to the cache for a specific attacker-target pair
    /// </summary>
    /// <param name="key">The cache key for the attacker-target pair</param>
    /// <param name="data">The evaluation data to cache</param>
    void AddTargetEvaluation(TargetEvaluationKey key, TargetEvaluationData data);
}
