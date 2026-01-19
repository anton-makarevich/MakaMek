using Microsoft.Extensions.AI;

namespace BotAgent.Services;

/// <summary>
/// Interface for LLM provider abstraction using Microsoft Agent Framework.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Get the ChatClient for creating ChatAgents.
    /// </summary>
    /// <returns>Configured IChatClient instance.</returns>
    IChatClient GetChatClient();
}
