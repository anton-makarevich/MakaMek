using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Tools.BotAgent.Services.LlmProviders;

namespace Sanet.MakaMek.Tools.BotAgent.Models.Agents;

/// <summary>
/// Deployment phase agent - selects optimal deployment position and facing for units.
/// Uses Microsoft Agent Framework's structured output for type-safe decisions.
/// </summary>
public class DeploymentAgent : BaseAgent
{
    public override string Name => "DeploymentAgent";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in unit deployment. Your role is to select
        optimal deployment positions and facing directions for your units.
        
        CRITICAL REQUIREMENTS:
        - You can ONLY deploy units that are marked as "UNDEPLOYED" in the YOUR UNITS section
        - If ALL units are already DEPLOYED or no units exist, you MUST return an error response
        - Each decision is for ONE unit only
        - USE get_deployment_zones tool, all positions must be within valid deployment zones 
        - Facing direction must be an integer 0-5
        
        TACTICAL PRINCIPLES:
        - Face toward enemies if they are deployed, otherwise toward map center
        - Consider unit role based on mass:
          * Heavy mechs (70-100 tons): Forward positions for frontline combat
          * Medium mechs (40-55 tons): Flexible mid-range positions
          * Light mechs (20-35 tons): Flanking positions with mobility options
        - Spread units to avoid clustering - maintain tactical spacing
        - Units should never deploy on the same hex. Note positions occupied by deployed units in both YOUR UNITS and ENEMY UNITS section and exclude them from available list of hexes.
        
        DECISION PROCESS:
        1. Check if ANY undeployed units exist - if not (YOUR UNITS is empty/none or all the units in it are DEPLOYED), return error response
        2. Identify which unit to deploy:
           - Use the unit specified in "DEPLOY UNIT:" if present
           - Otherwise, select the first undeployed unit from YOUR UNITS
        3. Call get_deployment_zones tool to get valid positions, return error if the tool is not available or returns no valid positions
        4. Analyze tactical situation (enemy positions, map center, terrain)
        5. Select optimal position from valid deployment zones
        6. Calculate facing direction (0-5) toward primary threat or objective
        7. Once you have all the information to make a decision, record it using make_deployment_decision tool
        
        BATTLETECH MAP INFO:
        - The map contains of hexes, each hex has a position defined by Q (column) and R (row) coordinates
        - The TopLeft (North-west) hex has Q=1, R=1 coordinates
        - The Left edge of the map has Q=1, the Right edge of the map has Q=map width
        - The Top edge of the map has R=1, the Bottom edge of the map has R=map height
        
        FACING DIRECTION VALUES:
        0 = North (Top)
        1 = Northeast (TopRight)
        2 = Southeast (BottomRight)
        3 = South (Bottom)
        4 = Southwest (BottomLeft)
        5 = Northwest (TopLeft)
        
        VALIDATION CHECKLIST (before responding):
        ✓ Is there at least one UNDEPLOYED unit available?
        ✓ Is unitId a valid GUID in a valid format from an UNDEPLOYED unit in YOUR UNITS?
        ✓ Is position within the deployment zones returned by get_deployment_zones?
        ✓ Are q and r integers?
        ✓ Is direction an integer between 0 and 5?
        ✓ Does reasoning explain the tactical choice clearly?
        """;

    public DeploymentAgent(
        ILlmProvider llmProvider,
        ILogger<DeploymentAgent> logger)
        : base(llmProvider, logger)
    {
    }

    /// <summary>
    /// Make the actual deployment decision using the provided agent.
    /// </summary>
    protected override async Task<DecisionResponse> GetAgentDecision(AIAgent agent,
        AgentThread thread,
        DecisionRequest request,
        string[] availableTools,
        CancellationToken cancellationToken)
    {
        try
        {
            PendingDecision = null;
            
            if (!availableTools.Contains("get_deployment_zones"))
            {
                throw new InvalidOperationException("get_deployment_zones tool is not available");
            }

            // Build user prompt with game context from DecisionRequest
            var userPrompt = BuildUserPrompt(request);

            // Run agent 
            var response = await agent.RunAsync(
                userPrompt, 
                thread,
                cancellationToken: cancellationToken);
            
            Logger.LogInformation("{AgentName} received response: {Response}", Name, response);

            // Map structured output to DecisionResponse
            return CreateDecisionResponse(request, response);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("INVALID_"))
        {
            Logger.LogError(ex, "{AgentName} validation error", Name);
            return CreateErrorResponse(ex.Message, ex.InnerException?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in {AgentName} decision making", Name);
            return CreateErrorResponse("AGENT_ERROR", ex.Message);
        }
        finally
        {
            Logger.LogDebug("DeploymentAgent decision making completed, the full thread {Thread}", thread.Serialize());
        }
    }

    /// <summary>
    /// Maps the structured LLM output to DecisionResponse with validation.
    /// </summary>
    private DecisionResponse CreateDecisionResponse(
        DecisionRequest request,
        AgentRunResponse response
        )
    {
        // Validate direction range
        if (PendingDecision?.Item1 is not DeployUnitCommand command)
        {
            Logger.LogError("Agent decision is null, agent response is {Response}", response);
            throw new InvalidOperationException("INVALID_DECISION");
        }
        
        command = command with { PlayerId = request.PlayerId };

        Logger.LogInformation(
            "{AgentName} created DeployUnitCommand - PlayerId: {PlayerId}, UnitId: {UnitId}",
            Name,
            command.PlayerId,
            command.UnitId);

        return new DecisionResponse(
            Success: true,
            Command: command,
            Reasoning: PendingDecision.Value.Item2,
            ErrorType: null,
            ErrorMessage: null,
            FallbackRequired: false
        );
    }

    protected override List<AITool> GetLocalTools()
    {
        return [
            AIFunctionFactory.Create(MakeDeploymentDecision, "make_deployment_decision")
        ];
    }
    
    [Description("Execute a deployment decision for a unit")]
    private string MakeDeploymentDecision(
        [Description("Unit GUID")] Guid unitId,
        [Description("Q coordinate")] int q,
        [Description("R coordinate")] int r,
        [Description("Facing direction 0-5")] int direction,
        [Description("Tactical reasoning")] string reasoning)
    {
        // Create and store the command
        var command = new DeployUnitCommand
        {
            UnitId = unitId,
            Position = new HexCoordinateData(q, r),
            Direction = direction,
            GameOriginId = Guid.Empty, // Will be set by ClientGame
        };
        PendingDecision = new ValueTuple<IClientCommand, string>(command, reasoning);
    
        return JsonSerializer.Serialize(new { 
            success = true, 
            message = "Deployment decision recorded" 
        });
    }

    /// <summary>
    /// Build user prompt with game context for deployment decisions.
    /// </summary>
    protected override string BuildUserPrompt(DecisionRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Make a unit deployment decision.");
        sb.AppendLine();

        // Add controlled units information
        if (request.ControlledUnits.Count > 0)
        {
            sb.AppendLine("YOUR UNITS:");
            foreach (var unit in request.ControlledUnits)
            {
                var deployStatus = unit.State.Position != null 
                    ? $"DEPLOYED at Position: Q={unit.State.Position.Coordinates.Q}, R={unit.State.Position.Coordinates.R}" 
                    : "UNDEPLOYED";
                sb.AppendLine($"- {unit.Model} ({unit.Mass} tons) - {deployStatus}");

                if (unit.Id.HasValue)
                    sb.AppendLine($"  ID: {unit.Id.Value}");
            }
        }
        else
        {
            sb.AppendLine("YOUR UNITS: (none)");
        }
        sb.AppendLine();

        // Add enemy units information
        if (request.EnemyUnits.Count > 0)
        {
            sb.AppendLine("ENEMY UNITS:");
            foreach (var enemy in request.EnemyUnits)
            {
                sb.AppendLine($"- {enemy.Model} ({enemy.Mass} tons)");
                if (enemy.State.Position != null)
                    sb.AppendLine($"  Position: Q={enemy.State.Position.Coordinates.Q}, R={enemy.State.Position.Coordinates.R}");
            }
        }
        else
        {
            sb.AppendLine("ENEMY UNITS: (none)");
        }
        sb.AppendLine();

        // Add a specific unit to deploy if specified
        if (request.UnitToAct.HasValue)
        {
            sb.AppendLine($"DEPLOY UNIT: {request.UnitToAct.Value}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
