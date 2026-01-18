namespace BotAgent.Models;

/// <summary>
/// Internal model for game state context (not shared with Integration Bot).
/// </summary>
public record GameContext(
    Guid GameId,
    int Turn,
    string Phase,
    Guid ActivePlayerId
);
