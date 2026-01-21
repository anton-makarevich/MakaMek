using System.ComponentModel;
using BotAgent.Services.LlmProviders;
using Microsoft.Agents.AI;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace BotAgent.Models.Agents;

/// <summary>
/// Movement phase agent - selects optimal movement destination and mode.
/// </summary>
public class MovementAgent : BaseAgent
{
    public override string Name => "MovementAgent";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in unit movement. Your goal is to position units to maximize offensive potential (hit probabilities, rear shots) and defensive safety (evasion, cover, armor facing).

        TACTICAL PROCESS:
        1. Identify the unit to move. If "MOVE UNIT" is present use that unit, if not - select a unit based on the tactical situation and their capabilities.
        2. Check unit status:
           - IF PRONE: Decide whether to Stand Up (if safe/possible) or Move Prone. Use 'make_standup_decision' if standing up.
           - IF STANDING: Proceed to standard movement.
        3. Call 'get_reachable_hexes(unitId)' to get all valid move options.
           - This returns options grouped by hex (Q, R), with 'OffensiveIndex' (higher is better - higher chance to hit an enemy from that position) and 'DefensiveIndex' (lower is better - lower chance to be hit by an enemy at this position).
        4. Select the best option based on:
           - Indices: Look for High Offensive + Lower Defensive values, prioritise Indices based on the situation and strategy - rather you want to play more defensive or aggressive.
        5. Call 'get_movement_path(unitId, q, r, movementType, facing)' to get the exact path.
           - 'facing' must be the final facing direction (0-5).
        6. Call 'make_movement_decision' with the retrieved path.

        FALLBACK / SKIP:
        - If 'get_reachable_hexes' returns nothing, or you cannot make a valid move, or you decide to Stand Still:
          Call 'make_movement_decision' with:
            - movementType = "StandingStill" (0)
            - pathSegments = [] (empty list)
            - reasoning = "No valid moves / Skipping turn"

        FACING DIRECTION VALUES:
        0 = North, 1 = Northeast, 2 = Southeast, 3 = South, 4 = Southwest, 5 = Northwest
        
        UNIT TYPES AND MOVEMENT ORDER:
        Initiative-Based Strategy
        **If moving first (lost initiative):**
        - Adopt defensive posture
        - Maximize movement modifiers
        - Position for over-watch
        
        **If moving last (won initiative):**
        - Take aggressive positioning
        - Strike at exposed enemies
        - Capitalize on enemy positioning mistakes
        
        Movement Order Optimization
        1. Move predictable/committed units first (LRM boats in sniper nests)
        2. Fast/mobile units move next to last
        3. Fallen units move last
        4. Keeps opponent guessing on key unit positioning

        VALIDATION:
        - unitId must match the one of the YOUR UNITS unit.
        - movementType must be one of: "StandingStill"(0), "Walk"(1), "Run"(2), "Jump"(3).
        - pathSegments must come from 'get_movement_path' (unless StandingStill).
        """;

    public MovementAgent(
        ILlmProvider llmProvider,
        ILogger<MovementAgent> logger)
        : base(llmProvider, logger)
    {
    }

    protected override async Task<DecisionResponse> GetAgentDecision(AIAgent agent,
        DecisionRequest request,
        string[] availableTools,
        CancellationToken cancellationToken)
    {
        var thread = agent.GetNewThread();
        try
        {
            PendingDecision = null;
            
            // Validate tools
            if (!availableTools.Contains("get_reachable_hexes") || !availableTools.Contains("get_movement_path"))
            {
                 throw new InvalidOperationException("Required movement tools are not available");
            }

            var userPrompt = BuildUserPrompt(request);

            var response = await agent.RunAsync(
                userPrompt, 
                thread,
                cancellationToken: cancellationToken);
            
            Logger.LogInformation("{AgentName} received response: {Response}", Name, response);

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

        command = command switch
        {
            // Ensure PlayerId is set
            MoveUnitCommand moveCmd => moveCmd with { PlayerId = request.PlayerId },
            TryStandupCommand standupCmd => standupCmd with { PlayerId = request.PlayerId },
            _ => throw new InvalidOperationException($"INVALID_COMMAND_TYPE: {command.GetType().Name}")
        };

        Logger.LogInformation("{AgentName} created command: {CommandType}", Name, command.GetType().Name);

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
            AIFunctionFactory.Create(MakeMovementDecision, "make_movement_decision"),
            AIFunctionFactory.Create(MakeStandupDecision, "make_standup_decision")
        ];
    }

    [Description("Execute a movement decision")]
    private string MakeMovementDecision(
        [Description("Unit GUID")] Guid unitId,
        [Description("Movement Type: 0=StandingStill, 1=Walk, 2=Run, 3=Jump")]
        int movementType,
        [Description("Path segments")] List<PathSegmentData> pathSegments,
        [Description("Reasoning")] string reasoning)
    {

        if (!Enum.IsDefined(typeof(MovementType), movementType) ||
            (MovementType)movementType == MovementType.Prone)
            throw new ArgumentException(
                $"Invalid movement type for movement decision: {movementType}. Must be StandingStill, Walk, Run, or Jump.");

        if (movementType != 0 && (pathSegments == null || pathSegments.Count == 0)) // if not StandingStill
        {
            throw new ArgumentException("Movement requires a non-empty pathSegments list.");
        }

        var command = new MoveUnitCommand
        {
            UnitId = unitId,
            MovementType = (MovementType)movementType,
            MovementPath = pathSegments,
            GameOriginId = Guid.Empty
        };

        PendingDecision = new ValueTuple<IClientCommand, string>(command, reasoning);
        return JsonSerializer.Serialize(new { success = true, message = "Movement decision recorded" });
    }

    [Description("Execute a stand-up decision (for prone units)")]
    private string MakeStandupDecision(
        [Description("Unit GUID")] Guid unitId,
        [Description("Movement Type after standup")]
        int movementType,
        [Description("New Facing Direction (0-5)")]
        int facing,
        [Description("Reasoning")] string reasoning)
    {
        if (!Enum.IsDefined(typeof(MovementType), movementType) ||
            (MovementType)movementType == MovementType.Prone)
            throw new ArgumentException(
                $"Invalid movement type after standup: {movementType}. Must be StandingStill, Walk, Run, or Jump.");
        if (facing is < 0 or > 5)
            throw new ArgumentException($"Invalid facing direction: {facing}. Must be 0-5.");

        var command = new TryStandupCommand
        {
            UnitId = unitId,
            MovementTypeAfterStandup = (MovementType)movementType,
            NewFacing = (HexDirection)facing,
            GameOriginId = Guid.Empty,
            PlayerId = Guid.Empty // Will be set later
        };

        PendingDecision = new ValueTuple<IClientCommand, string>(command, reasoning);
        return JsonSerializer.Serialize(new { success = true, message = "Standup decision recorded" });
    }

    protected override string BuildUserPrompt(DecisionRequest request)
    {
         var sb = new StringBuilder();
        sb.AppendLine($"Make a tactical movement decision.");
        sb.AppendLine();
        
        if (request.UnitToAct.HasValue)
        {
            sb.AppendLine($"MOVE UNIT: {request.UnitToAct.Value}");
            var unit = request.ControlledUnits.FirstOrDefault(u => u.Id == request.UnitToAct.Value);
            sb.AppendLine($"Status: {(unit.StatusFlags?.Contains(UnitStatus.Prone)==true ? "PRONE" : "STANDING")}");
            if (unit.Position != null)
                sb.AppendLine($"Current Position: Q={unit.Position.Coordinates.Q}, R={unit.Position.Coordinates.R}, Facing={unit.Position.Facing}");
                
            // Add Movement Mode info if available in UnitData, otherwise inferred by Agent via tools
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("YOUR UNITS to move:");
            foreach (var unit in request.ControlledUnits.Where(u => u is { Id: not null, MovementPathSegments: null }))
            {
                sb.AppendLine($"ID: {unit.Id}");
                sb.AppendLine($"Status: {(unit.StatusFlags?.Contains(UnitStatus.Prone)==true ? "PRONE" : "STANDING")}");
                sb.AppendLine($"- {unit.Model} ({unit.Mass} tons)");
                if (unit.Position != null)
                    sb.AppendLine($"  Position: Q={unit.Position.Coordinates.Q}, R={unit.Position.Coordinates.R}, Facing={unit.Position.Facing}");
            }
        }

        // Enemy info
        if (request.EnemyUnits.Count > 0)
        {
            sb.AppendLine("ENEMY UNITS:");
            foreach (var enemy in request.EnemyUnits)
            {
                sb.AppendLine($"- {enemy.Model} ({enemy.Mass} tons)");
                if (enemy.Position != null)
                    sb.AppendLine($"  Position: Q={enemy.Position.Coordinates.Q}, R={enemy.Position.Coordinates.R}");
            }
        }
        
        return sb.ToString();
    }
}
