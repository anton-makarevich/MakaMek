using System.Collections.Concurrent;
using Sanet.MakaMek.Bots.Data;

namespace Sanet.MakaMek.Bots.Models;

/// <summary>
/// Manages turn-specific cached data for bot decision-making to optimize performance
/// across movement and weapons phases
/// </summary>
public class TurnState : ITurnState
{
    /// <summary>
    /// The game ID this turn state is associated with
    /// </summary>
    public Guid GameId { get; }
    
    /// <summary>
    /// The turn number this state is for
    /// </summary>
    public int TurnNumber { get; }
    
    // Key: (AttackerId, AttackerPositionDetails, TargetId, TargetPositionDetails)
    // Value: Cached evaluation data (List of target evaluation data for that specific pair)
    private readonly ConcurrentDictionary<TargetEvaluationKey, TargetEvaluationData> _targetEvaluationCache = new();

    public TurnState(Guid gameId, int turnNumber)
    {
        GameId = gameId;
        TurnNumber = turnNumber;
    }

    /// <summary>
    /// Attempts to get cached target evaluation data for a specific attacker-target pair
    /// </summary>
    /// <param name="key">The cache key for the attacker-target pair</param>
    /// <param name="data">The cached evaluation data if found</param>
    /// <returns>True if cached data was found, false otherwise</returns>
    public bool TryGetTargetEvaluation(TargetEvaluationKey key, out TargetEvaluationData data) 
    { 
        return _targetEvaluationCache.TryGetValue(key, out data); 
    }

    /// <summary>
    /// Adds target evaluation data to the cache for a specific attacker-target pair
    /// </summary>
    /// <param name="key">The cache key for the attacker-target pair</param>
    /// <param name="data">The evaluation data to cache</param>
    public void AddTargetEvaluation(TargetEvaluationKey key, TargetEvaluationData data) 
    {
        _targetEvaluationCache.TryAdd(key, data);
    }
}
