using BotAgent.Services;

namespace BotAgent.Models.Agents;

/// <summary>
/// Movement phase agent - evaluates movement options and selects optimal path.
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
        McpClientService mcpClient,
        ILogger<MovementAgent> logger)
        : base(llmProvider, mcpClient, logger)
    {
    }
}
