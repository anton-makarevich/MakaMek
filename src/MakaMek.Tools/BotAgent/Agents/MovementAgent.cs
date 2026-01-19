using BotAgent.Models;
using BotAgent.Services;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Units;

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
        position units for maximum tactical advantage while minimizing risk.
        Consider standing up if unit is prone (TryStandupCommand).
        Consider moving to better position if standing (MoveUnitCommand).
        Consider:
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

    protected override IClientCommand ParseDecision(string responseText, DecisionRequest request)
    {
        // TODO: Parse actual JSON response from LLM
        // TODO: Determine if we need to stand up or move based on LLM output
        
        // Placeholder: Default to MoveUnitCommand
        return new MoveUnitCommand
        {
            PlayerId = request.PlayerId,
            UnitId = Guid.Empty, // Would come from LLM/Context
            GameOriginId = Guid.Empty, // Would come from Context
            MovementType = MovementType.Walk,
            MovementPath = new List<PathSegmentData>(),
            IdempotencyKey = Guid.NewGuid()
        };
    }
}
