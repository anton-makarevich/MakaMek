using BotAgent.Models;
using BotAgent.Services;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Map;

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
        select deployment positions that maximize tactical advantage.
        Analyze the available deployment zones and select the best position and facing.
        """;

    public DeploymentAgent(
        ILlmProvider llmProvider,
        McpClientService mcpClient,
        ILogger<DeploymentAgent> logger)
        : base(llmProvider, mcpClient, logger)
    {
    }

    protected override IClientCommand ParseDecision(string responseText, DecisionRequest request)
    {
        // TODO: Parse actual JSON response from LLM
        // This is a placeholder that returns a dummy command
        
        return new DeployUnitCommand
        {
            PlayerId = request.PlayerId,
            UnitId = Guid.Empty, // Would come from LLM/Context
            GameOriginId = Guid.Empty, // will be set by client game
            Position = new HexCoordinateData(0, 0),
            Direction = 0,
            IdempotencyKey = Guid.NewGuid()
        };
    }
}
