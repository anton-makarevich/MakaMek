using System.ClientModel;
using BotAgent.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace BotAgent.Services.LlmProviders;

/// <summary>
/// Local OpenAI-like provider that uses the local client supporting OpenAI API.
/// </summary>
public class LocalOpenAiLikeProvider : ILlmProvider
{
    private readonly LlmProviderConfiguration _config;
    private readonly ILogger<LocalOpenAiLikeProvider> _logger;
    private readonly Uri _endpoint;

    public LocalOpenAiLikeProvider(
        IOptions<LlmProviderConfiguration> config,
        ILogger<LocalOpenAiLikeProvider> logger)
    {
        _config = config.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_config.Endpoint))
        {
            throw new InvalidOperationException(
                "LocalOpenAILikeProvider requires LlmProvider:Endpoint to be set.");
        }

        if (!Uri.TryCreate(_config.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException(
                $"Invalid LlmProvider:Endpoint value '{_config.Endpoint}'.");
        }

        _endpoint = endpoint;
    }

    public IChatClient GetChatClient()
    {
        _logger.LogDebug("Creating OpenAI-like ChatClient for model: {Model}", _config.Model);

        // Create OpenAI client
        var openAiClient = new OpenAIClient(
            new ApiKeyCredential("NO_API_KEY_REQUIRED"),
            new OpenAIClientOptions{ Endpoint = _endpoint });
        var chatClient = openAiClient.GetChatClient(_config.Model);

        // Create ChatClient adapter using extension method
        return chatClient.AsIChatClient();
    }
}
