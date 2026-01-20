using BotAgent.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace BotAgent.Services.LlmProviders;

/// <summary>
/// OpenAI implementation of an LLM provider using Microsoft Agent Framework.
/// </summary>
public class OpenAiProvider : ILlmProvider
{
    private readonly LlmProviderConfiguration _config;
    private readonly ILogger<OpenAiProvider> _logger;

    public OpenAiProvider(
        IOptions<LlmProviderConfiguration> config,
        ILogger<OpenAiProvider> logger)
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
