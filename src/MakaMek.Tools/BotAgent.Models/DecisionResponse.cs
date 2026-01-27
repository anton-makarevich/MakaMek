using Sanet.MakaMek.Core.Data.Game.Commands;

namespace Sanet.MakaMek.Tools.BotAgent.Models;

/// <summary>
/// Response from LLM Agent to Integration Bot with tactical decision or error.
/// </summary>
/// <param name="Success">Indicates if the decision was successfully generated.</param>
/// <param name="Command">The IGameCommand object to execute.</param>
/// <param name="Reasoning">The LLM's reasoning for the decision (chain-of-thought).</param>
/// <param name="ErrorType">Type of error if Success is false (AGENT_CANNOT_DECIDE, LLM_TIMEOUT, INVALID_GAME_STATE).</param>
/// <param name="ErrorMessage">Detailed error message if Success is false.</param>
/// <param name="FallbackRequired">Indicates if the Integration Bot should use fallback engine.</param>
public record DecisionResponse(
    bool Success,
    IGameCommand? Command,
    string? Reasoning,
    string? ErrorType,
    string? ErrorMessage,
    bool FallbackRequired
);
