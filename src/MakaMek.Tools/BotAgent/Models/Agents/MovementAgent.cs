using BotAgent.Services.LlmProviders;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace BotAgent.Models.Agents;

/// <summary>
/// Movement phase agent - evaluates movement options and selects an optimal path.
/// </summary>
public class MovementAgent : BaseAgent
{
    public override string Name => "MovementAgent";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in mech movement. Your goal is to
        position units for maximum tactical advantage while minimizing risk.
        Consider standing up if unit is prone (TryStandupCommand).
        Consider moving to better position if standing (MoveUnitCommand).
        Consider:
        - Offensive positioning (weapon range, line of sight to enemies)
        - Defensive positioning (cover, minimizing rear arc exposure)
        - Heat management (walking vs running vs jumping)
        - Terrain effects (elevation, woods, water)
        - Piloting skill roll requirements
        
        Use tactical evaluation tools to score movement options and select the best path.
        """;

    public MovementAgent(
        ILlmProvider llmProvider,
        ILogger<MovementAgent> logger)
        : base(llmProvider, logger)
    {
    }

    protected override List<AITool> GetLocalTools()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Build user prompt with game context for movement decisions.
    /// </summary>
    protected override string BuildUserPrompt(DecisionRequest request) => 
        throw new NotImplementedException("BuildUserPrompt not yet implemented for this agent");

    /// <summary>
    /// Make the actual movement decision using the provided agent.
    /// </summary>
    protected override Task<DecisionResponse> GetAgentDecision(
        AIAgent agent, 
        DecisionRequest request,
        string[] availableTools,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(CreateErrorResponse("NOT_IMPLEMENTED", "MovementAgent not yet implemented"));
    }
}
