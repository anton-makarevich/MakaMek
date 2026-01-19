using System.Text.Json;
using BotAgent.Models;
using Microsoft.Extensions.Options;
using MakaMek.Tools.BotContainer.Configuration;

namespace MakaMek.Tools.BotContainer.Services;

/// <summary>
/// HTTP client for making decision requests to the BotAgent API.
/// </summary>
public class BotAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly BotAgentConfiguration _config;
    private readonly ILogger<BotAgentClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public BotAgentClient(
        HttpClient httpClient,
        IOptions<BotAgentConfiguration> config,
        ILogger<BotAgentClient> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Request a tactical decision from the BotAgent API.
    /// </summary>
    /// <param name="playerId">The player ID requesting the decision.</param>
    /// <param name="phase">The current game phase.</param>
    /// <param name="mcpServerUrl">The URL of the MCP server for game state queries.</param>
    /// <param name="timeout">Request timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decision response from the agent.</returns>
    public async Task<DecisionResponse> RequestDecisionAsync(
        Guid playerId,
        string phase,
        string mcpServerUrl,
        int timeout = 30000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DecisionRequest(
                PlayerId: playerId,
                Phase: phase,
                McpServerUrl: mcpServerUrl,
                Timeout: timeout
            );

            _logger.LogInformation(
                "Requesting decision from BotAgent for player {PlayerId} in phase {Phase}",
                playerId,
                phase);

            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(timeout + 5000)); // Add 5s buffer

            var response = await _httpClient.PostAsync(
                $"{_config.ApiUrl}/api/decision",
                content,
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "BotAgent API returned error status: {StatusCode}",
                    response.StatusCode);

                return new DecisionResponse(
                    Success: false,
                    Command: null,
                    Reasoning: null,
                    ErrorType: "HTTP_ERROR",
                    ErrorMessage: $"HTTP {response.StatusCode}",
                    FallbackRequired: true
                );
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var decisionResponse = JsonSerializer.Deserialize<DecisionResponse>(responseJson, _jsonOptions);

            if (decisionResponse == null)
            {
                _logger.LogError("Failed to deserialize DecisionResponse");
                return new DecisionResponse(
                    Success: false,
                    Command: null,
                    Reasoning: null,
                    ErrorType: "DESERIALIZATION_ERROR",
                    ErrorMessage: "Failed to deserialize response",
                    FallbackRequired: true
                );
            }

            _logger.LogInformation(
                "Received decision response - Success: {Success}, FallbackRequired: {FallbackRequired}",
                decisionResponse.Success,
                decisionResponse.FallbackRequired);

            return decisionResponse;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "BotAgent request timed out");
            return new DecisionResponse(
                Success: false,
                Command: null,
                Reasoning: null,
                ErrorType: "TIMEOUT",
                ErrorMessage: "Request timed out",
                FallbackRequired: true
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to BotAgent failed");
            return new DecisionResponse(
                Success: false,
                Command: null,
                Reasoning: null,
                ErrorType: "NETWORK_ERROR",
                ErrorMessage: ex.Message,
                FallbackRequired: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error requesting decision from BotAgent");
            return new DecisionResponse(
                Success: false,
                Command: null,
                Reasoning: null,
                ErrorType: "INTERNAL_ERROR",
                ErrorMessage: ex.Message,
                FallbackRequired: true
            );
        }
    }
}

