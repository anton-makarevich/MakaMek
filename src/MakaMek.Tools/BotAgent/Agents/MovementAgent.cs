using BotAgent.Models;
using BotAgent.Services;

namespace BotAgent.Agents;

/// <summary>
/// Movement phase agent - evaluates movement options and selects optimal path.
/// </summary>
public class MovementAgent : BaseAgent
{
    public override string Name => "MovementAgent";
    public override string Description => "Specialist in mech movement and tactical positioning";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in mech movement. Your goal is to
        position units for maximum tactical advantage while minimizing risk. Consider:
        - Offensive positioning (weapon range, line of sight to enemies)
        - Defensive positioning (cover, minimizing rear arc exposure)
        - Heat management (walking vs running vs jumping)
        - Terrain effects (elevation, woods, water)
        - Piloting skill roll requirements
        
        Use the tactical evaluation tools to score movement options and select the best path.
        """;

    public MovementAgent(
        ILlmProvider llmProvider,
        McpClientService mcpClient,
        ILogger<MovementAgent> logger)
        : base(llmProvider, mcpClient, logger)
    {
    }

    public override async Task<DecisionResponse> MakeDecisionAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("MovementAgent making decision for player {PlayerId}", request.PlayerId);

            // TODO: Query MCP Server for movement options and game state
            // var gameState = await McpClient.GetGameStateAsync(request.McpServerUrl, cancellationToken);
            // var movementOptions = await McpClient.EvaluateMovementOptionsAsync(request.McpServerUrl, unitId, cancellationToken);

            // TODO: Generate decision using LLM
            // var decision = await LlmProvider.GenerateDecisionAsync(SystemPrompt, userPrompt, cancellationToken);

            // Placeholder response until full implementation
            return CreateErrorResponse(
                "AGENT_NOT_IMPLEMENTED",
                "MovementAgent full implementation pending Integration Bot MCP Server");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in MovementAgent decision making");
            return CreateErrorResponse("AGENT_ERROR", ex.Message);
        }
    }
}
