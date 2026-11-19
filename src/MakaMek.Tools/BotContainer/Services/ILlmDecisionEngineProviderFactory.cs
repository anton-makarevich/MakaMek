using Sanet.MakaMek.Core.Models.Game;

namespace Sanet.MakaMek.Tools.BotContainer.Services;

/// <summary>
/// Creates the LLM-backed decision engine provider for the client game the bot has just connected to.
/// The game and the MCP server address are only known at connection time, so the provider is built
/// by a factory instead of being created inside the hosted service.
/// </summary>
public interface ILlmDecisionEngineProviderFactory
{
    /// <summary>
    /// Builds a decision engine provider bound to <paramref name="clientGame"/>.
    /// </summary>
    /// <param name="clientGame">The game the bot plays in.</param>
    /// <param name="mcpServerUrl">The URL of the MCP server exposing this bot's tools.</param>
    LlmDecisionEngineProvider Create(IClientGame clientGame, string mcpServerUrl);
}
