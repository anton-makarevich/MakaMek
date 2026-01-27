using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Tools.BotAgent.Services.LlmProviders;

namespace Sanet.MakaMek.Tools.BotAgent.Models.Agents;

/// <summary>
/// End phase agent - manages shutdown and startup decisions, ends turn.
/// </summary>
public class EndPhaseAgent : BaseAgent
{
    public override string Name => "EndPhaseAgent";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in end phase decisions.
        
        HEAT MANAGEMENT:
        - BattleMechs generate heat from movement and weapons.
        - High heat slows you down, makes it harder to hit, and risks automatic shutdown or ammo explosion.
        - Heat dissipates at the end of the turn (Dissipation).
        - Max heat is 30. Shutdown is AUTOMATIC at 30.
        
        DECISION LOGIC:
        1. Evaluate each of YOUR UNITS:
           - IF unit is active:
             * Check Current Heat.
             * If heat is dangerously high (e.g. > 24) or risks ammo explosion (heat 19, 23, 28 if has ammo), consider 'make_shutdown_decision'.
             * A shutdown unit becomes immobile and easy to hit but dissipates heat safely.
           - IF unit is shutdown:
             * Check Current Heat.
             * If heat is low enough to safely restart (suggested < 14 for automatic success, or higher if tactically necessary), consider 'make_startup_decision'.
             * You cannot restart in the same turn you shutdown.
        
        TURN COMPLETION:
        - If shutdown/startup decisions were not made, you MUST ALWAYS call 'make_turn_ended_decision'. This is mandatory to advance the game.
        - You can only make ONE (of three) decision per session
        
        TOOLS:
        - Use 'make_shutdown_decision' to voluntarily shut down a unit.
        - Use 'make_startup_decision' to attempt to restart a shutdown unit.
        - Use 'make_turn_ended_decision' to complete your turn.
        """;


    public EndPhaseAgent(
        ILlmProvider llmProvider,
        ILogger<EndPhaseAgent> logger)
        : base(llmProvider, logger)
    {
    }

    protected override List<AITool> GetLocalTools()
    {
        return [
            AIFunctionFactory.Create(MakeShutdownDecision, "make_shutdown_decision"),
            AIFunctionFactory.Create(MakeStartupDecision, "make_startup_decision"),
            AIFunctionFactory.Create(MakeTurnEndedDecision, "make_turn_ended_decision")
        ];
    }

    [Description("Execute a voluntary shutdown for a unit to manage extreme heat")]
    private string MakeShutdownDecision(
        [Description("Unit GUID")] Guid unitId,
        [Description("Tactical reasoning")] string reasoning)
    {
        var command = new ShutdownUnitCommand
        {
            UnitId = unitId,
            GameOriginId = Guid.Empty,
            PlayerId = Guid.Empty // Will be set later
        };

        PendingDecision = new ValueTuple<IClientCommand, string>(command, reasoning);
        return JsonSerializer.Serialize(new { success = true, message = "Shutdown decision recorded" });
    }

    [Description("Execute a startup attempt for a shutdown unit")]
    private string MakeStartupDecision(
        [Description("Unit GUID")] Guid unitId,
        [Description("Tactical reasoning")] string reasoning)
    {
        var command = new StartupUnitCommand
        {
            UnitId = unitId,
            GameOriginId = Guid.Empty,
            PlayerId = Guid.Empty // Will be set later
        };

        PendingDecision = new ValueTuple<IClientCommand, string>(command, reasoning);
        return JsonSerializer.Serialize(new { success = true, message = "Startup decision recorded" });
    }

    [Description("Signal that all end-phase decisions are complete for the turn")]
    private string MakeTurnEndedDecision(
        [Description("Tactical reasoning")] string reasoning)
    {
        var command = new TurnEndedCommand
        {
            GameOriginId = Guid.Empty,
            PlayerId = Guid.Empty // Will be set later
        };

        PendingDecision = new ValueTuple<IClientCommand, string>(command, reasoning);
        return JsonSerializer.Serialize(new { success = true, message = "Turn ended decision recorded" });
    }


    /// <summary>
    /// Build user prompt with game context for end-phase decisions.
    /// </summary>
    protected override string BuildUserPrompt(DecisionRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Make end-phase decisions (shutdown/startup/end turn).");
        sb.AppendLine();

        sb.AppendLine("YOUR UNITS:");
        foreach (var unit in request.ControlledUnits)
        {
            sb.AppendLine($"- {unit.Model} ({unit.Mass} tons)");
            if (unit.Id.HasValue)
                sb.AppendLine($"  ID: {unit.Id.Value}");

            if (unit.Position != null)
                sb.AppendLine($"  Position: Q={unit.Position.Coordinates.Q}, R={unit.Position.Coordinates.R}");
            
            sb.AppendLine($"  Current Heat: {unit.CurrentHeat} (Max: 30)");
            
            var isShutdown = unit.StatusFlags?.Contains(UnitStatus.Shutdown) == true;
            sb.AppendLine($"  Status: {(isShutdown ? "SHUTDOWN" : "ACTIVE")}");
        }

        sb.AppendLine();

        sb.AppendLine("ENEMY POSITIONS:");
        foreach (var enemy in request.EnemyUnits)
        {
            if (enemy.Position != null)
                sb.AppendLine($"- {enemy.Model}: Q={enemy.Position.Coordinates.Q}, R={enemy.Position.Coordinates.R}");
        }

        sb.AppendLine();
        return sb.ToString();
    }


    /// <summary>
    /// Make the actual end-phase decision using the provided agent.
    /// </summary>
    protected override async Task<DecisionResponse> GetAgentDecision(
        AIAgent agent, 
        AgentThread thread,
        DecisionRequest request, 
        string[] availableTools,
        CancellationToken cancellationToken)
    {
        try
        {
            PendingDecision = null;

            // Build the user prompt
            var userPrompt = BuildUserPrompt(request);

            var response = await agent.RunAsync(
                userPrompt,
                thread,
                cancellationToken: cancellationToken);

            Logger.LogInformation("{AgentName} received response: {Response}", Name, response);

            return CreateDecisionResponse(request, response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in {AgentName} decision making", Name);
            return CreateErrorResponse("AGENT_ERROR", ex.Message);
        }
    }

    private DecisionResponse CreateDecisionResponse(
        DecisionRequest request,
        AgentRunResponse response)
    {
        if (PendingDecision?.Item1 is not { } command)
        {
            Logger.LogError("Agent decision is null. Response: {Response}", response);
            throw new InvalidOperationException("INVALID_DECISION");
        }

        var reasoning = PendingDecision.Value.Item2;

        command = command switch
        {
            ShutdownUnitCommand shutdownCommand => shutdownCommand with { PlayerId = request.PlayerId },
            StartupUnitCommand startupCommand => startupCommand with { PlayerId = request.PlayerId },
            TurnEndedCommand turnEndedCommand => turnEndedCommand with { PlayerId = request.PlayerId },
            _ => throw new InvalidOperationException($"INVALID_COMMAND_TYPE: {command.GetType().Name}")
        };

        Logger.LogInformation("{AgentName} created command: {CommandType}", Name, command.GetType().Name);

        return new DecisionResponse(
            Success: true,
            Command: command,
            Reasoning: reasoning,
            ErrorType: null,
            ErrorMessage: null,
            FallbackRequired: false
        );
    }

}
