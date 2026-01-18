using BotAgent.Models;
using BotAgent.Services;

namespace BotAgent.Agents;

/// <summary>
/// End phase agent - manages shutdown and startup decisions, ends turn.
/// </summary>
public class EndPhaseAgent : BaseAgent
{
    public override string Name => "EndPhaseAgent";
    public override string Description => "Specialist in heat management and end phase decisions";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in end phase decisions. Your goal is to
        keep units operational while avoiding catastrophic heat effects. Consider:
        - Current heat level and dissipation rate
        - Ammo explosion risk at high heat
        - Shutdown vs continued operation trade-offs
        - Restart probability for shutdown units
        - Next turn tactical requirements
        
        Make decisions about shutting down overheated units or restarting shutdown units.
        """;

    public EndPhaseAgent(
        ILlmProvider llmProvider,
        McpClientService mcpClient,
        ILogger<EndPhaseAgent> logger)
        : base(llmProvider, mcpClient, logger)
    {
    }

    public override async Task<DecisionResponse> MakeDecisionAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("EndPhaseAgent making decision for player {PlayerId}", request.PlayerId);

            // TODO: Query MCP Server for unit details and game state
            // var gameState = await McpClient.GetGameStateAsync(request.McpServerUrl, cancellationToken);
            // var unitDetails = await McpClient.GetUnitDetailsAsync(request.McpServerUrl, unitId, cancellationToken);

            // TODO: Generate decision using LLM
            // var decision = await LlmProvider.GenerateDecisionAsync(SystemPrompt, userPrompt, cancellationToken);

            // Placeholder response until full implementation
            return CreateErrorResponse(
                "AGENT_NOT_IMPLEMENTED",
                "EndPhaseAgent full implementation pending Integration Bot MCP Server");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in EndPhaseAgent decision making");
            return CreateErrorResponse("AGENT_ERROR", ex.Message);
        }
    }
}
