using BotAgent.Services;

namespace BotAgent.Models.Agents;

/// <summary>
/// End phase agent - manages shutdown and startup decisions, ends turn.
/// </summary>
public class EndPhaseAgent : BaseAgent
{
    public override string Name => "EndPhaseAgent";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in end phase decisions.
        Consider shutting down overheated units (ShutdownUnitCommand).
        Consider restarting shutdown units (StartupUnitCommand).
        End turn (TurnEndedCommand) when all unit management is done.
        """;

    public EndPhaseAgent(
        ILlmProvider llmProvider,
        McpClientService mcpClient,
        ILogger<EndPhaseAgent> logger)
        : base(llmProvider, mcpClient, logger)
    {
    }
}
