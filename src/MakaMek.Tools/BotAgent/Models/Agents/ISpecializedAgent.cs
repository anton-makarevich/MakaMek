namespace BotAgent.Models.Agents;

/// <summary>
/// Interface for specialized agents that handle specific game phases.
/// </summary>
public interface ISpecializedAgent
{
    /// <summary>
    /// Unique name of the agent.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Make a tactical decision for the given request.
    /// </summary>
    /// <param name="request">Decision request from Integration Bot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decision response with command or error.</returns>
    Task<DecisionResponse> MakeDecisionAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default);
}
