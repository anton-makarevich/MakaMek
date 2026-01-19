using BotAgent.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace BotAgent.Services;

/// <summary>
/// OpenAI implementation of LLM provider using Microsoft Agent Framework.
/// </summary>
public class OpenAIProvider : ILlmProvider
{
    private readonly LlmProviderConfiguration _config;
    private readonly ILogger<OpenAIProvider> _logger;

    public OpenAIProvider(
        IOptions<LlmProviderConfiguration> config,
        ILogger<OpenAIProvider> logger)
    {
        _config = config.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is not configured. Set LlmProvider:ApiKey in configuration or OPENAI_API_KEY environment variable.");
        }
    }

    public IChatClient GetChatClient()
    {
        _logger.LogDebug("Creating OpenAI ChatClient for model: {Model}", _config.Model);

        // Create OpenAI client
        var openAiClient = new OpenAIClient(_config.ApiKey);
        var chatClient = openAiClient.GetChatClient(_config.Model);

        // Create ChatClient adapter using extension method
        return chatClient.AsIChatClient();
    }
}
