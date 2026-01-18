using BotAgent.Models;
using BotAgent.Services;

namespace BotAgent.Agents;

/// <summary>
/// Deployment phase agent - selects optimal deployment position and facing for units.
/// </summary>
public class DeploymentAgent : BaseAgent
{
    public override string Name => "DeploymentAgent";
    public override string Description => "Specialist in unit deployment and initial positioning";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in unit deployment. Your goal is to
        select deployment positions that maximize tactical advantage while considering:
        - Distance to enemy units
        - Terrain cover and elevation
        - Line of sight to objectives
        - Support from friendly units
        - Escape routes and maneuverability
        
        Analyze the available deployment zones and select the best position and facing.
        """;

    public DeploymentAgent(
        ILlmProvider llmProvider,
        McpClientService mcpClient,
        ILogger<DeploymentAgent> logger)
        : base(llmProvider, mcpClient, logger)
    {
    }

    public override async Task<DecisionResponse> MakeDecisionAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("DeploymentAgent making decision for player {PlayerId}", request.PlayerId);

            // TODO: Query MCP Server for deployment zones and game state
            // var gameState = await McpClient.GetGameStateAsync(request.McpServerUrl, cancellationToken);

            // TODO: Generate decision using LLM
            // var decision = await LlmProvider.GenerateDecisionAsync(SystemPrompt, userPrompt, cancellationToken);

            // Placeholder response until full implementation
            return CreateErrorResponse(
                "AGENT_NOT_IMPLEMENTED",
                "DeploymentAgent full implementation pending Integration Bot MCP Server");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in DeploymentAgent decision making");
            return CreateErrorResponse("AGENT_ERROR", ex.Message);
        }
    }
}
