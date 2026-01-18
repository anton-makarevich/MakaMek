namespace BotAgent.Services;

/// <summary>
/// Interface for LLM provider abstraction.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Generate a tactical decision using the LLM with structured output.
    /// </summary>
    /// <param name="systemPrompt">System prompt defining agent role and instructions.</param>
    /// <param name="userPrompt">User prompt with game context and decision request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>LLM response as structured JSON string.</returns>
    Task<string> GenerateDecisionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate chain-of-thought reasoning for a decision.
    /// </summary>
    /// <param name="systemPrompt">System prompt defining agent role.</param>
    /// <param name="userPrompt">User prompt with tactical situation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Reasoning text.</returns>
    Task<string> GenerateReasoningAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
