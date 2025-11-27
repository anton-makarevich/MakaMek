namespace Sanet.MakaMek.Bots.Exceptions;

/// <summary>
/// Exception thrown when a bot cannot make a valid decision
/// </summary>
public class BotDecisionException : Exception
{
    /// <summary>
    /// The type of decision engine that failed to make a decision
    /// </summary>
    public string DecisionEngineType { get; }
    
    /// <summary>
    /// The ID of the player for whom the decision failed
    /// </summary>
    public Guid PlayerId { get; }
    
    public BotDecisionException(string message, string decisionEngineType, Guid playerId) 
        : base(message)
    {
        DecisionEngineType = decisionEngineType;
        PlayerId = playerId;
    }
    
    public BotDecisionException(string message, string decisionEngineType, Guid playerId, Exception innerException) 
        : base(message, innerException)
    {
        DecisionEngineType = decisionEngineType;
        PlayerId = playerId;
    }
}
