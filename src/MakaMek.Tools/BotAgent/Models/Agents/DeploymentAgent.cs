using BotAgent.Models.Agents.Outputs;
using BotAgent.Services;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;

namespace BotAgent.Models.Agents;

/// <summary>
/// Deployment phase agent - selects optimal deployment position and facing for units.
/// Uses Microsoft Agent Framework's structured output (RunAsync<T>) for type-safe decisions.
/// </summary>
public class DeploymentAgent : BaseAgent
{
    public override string Name => "DeploymentAgent";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in unit deployment. Your role is to select
        optimal deployment positions and facing directions for your units. You make a decision for only one unit at a time.

        TACTICAL PRINCIPLES:
        - Deploy in valid deployment zones 
        - Face toward enemies if deployed, otherwise toward map center
        - Consider unit role:
          * Heavy mechs (70-100 tons): Deploy forward for frontline combat
          * Medium mechs (40-55 tons): Deploy for tactical flexibility
          * Light mechs (20-35 tons): Deploy for flanking and mobility
        - Avoid clustering - spread units for tactical flexibility
        - Use terrain for advantage when available (cover, elevation)
        - Maintain line of sight to expected engagement areas

        AVAILABLE INFORMATION:
        - Controlled units: Your units with deployment status
        - Enemy units: Enemy units with positions (if deployed)
        - Valid deployment zones: use get_deployment_zones tool  // add once implemented

        DECISION PROCESS:
        1. Identify which unit to deploy (first undeployed unit if not specified)
        2. Analyze valid deployment positions
        3. Consider enemy positions and map center
        4. Select position that maximizes tactical advantage
        5. Calculate optimal facing direction

        OUTPUT FORMAT:
        You must respond with a structured JSON object containing:
        {
          "unitId": "guid-string",
          "position": {"q": int, "r": int},
          "direction": int (0-5),
          "reasoning": "brief tactical explanation"
        }

        Direction (facing) values:
        0 = Top, 1 = TopRight, 2 = BottomRight, 3 = Bottom, 4 = BottomLeft, 5 = TopLeft
        
        IMPORTANT: Ensure:
        - unitId is a valid GUID string from the provided unit list (a unit we are going to deploy)
        - position Q and R are integers within map bounds
        - direction is an integer between 0 and 5 (inclusive)
        - reasoning clearly explains the tactical rationale
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
            Logger.LogInformation("{AgentName} making decision for player {PlayerId}", Name, request.PlayerId);

            // Build user prompt with game context from DecisionRequest
            var userPrompt = BuildUserPrompt(request);

            // Run agent with structured output using MAF
            var structuredResponse = await Agent.RunAsync<DeploymentAgentOutput>(
                userPrompt, 
                cancellationToken: cancellationToken);

            Logger.LogInformation(
                "{AgentName} received structured output - Unit: {UnitId}, Position: ({Q},{R}), Direction: {Direction}",
                Name,
                structuredResponse.Result.UnitId,
                structuredResponse.Result.Position.Q,
                structuredResponse.Result.Position.R,
                structuredResponse.Result.Direction);

            // Map structured output to DecisionResponse
            return MapToDecisionResponse(structuredResponse.Result, request);
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

    /// <summary>
    /// Maps the structured LLM output to DecisionResponse with validation.
    /// </summary>
    private DecisionResponse MapToDecisionResponse(
        DeploymentAgentOutput output, 
        DecisionRequest request)
    {
        // Validate GUID
        if (!Guid.TryParse(output.UnitId, out var unitId))
        {
            Logger.LogError("Invalid unitId in LLM output: {UnitId}", output.UnitId);
            throw new InvalidOperationException("INVALID_UNIT_ID");
        }

        // Validate direction range
        if (output.Direction < 0 || output.Direction > 5)
        {
            Logger.LogError("Invalid direction in LLM output: {Direction}", output.Direction);
            throw new InvalidOperationException("INVALID_DIRECTION");
        }

        // Create command
        var command = new DeployUnitCommand
        {
            PlayerId = request.PlayerId,
            UnitId = unitId,
            GameOriginId = Guid.Empty, // Will be set by ClientGame
            Position = output.Position,
            Direction = output.Direction,
            IdempotencyKey = null // Will be set by ClientGame
        };

        Logger.LogInformation(
            "{AgentName} created DeployUnitCommand - PlayerId: {PlayerId}, UnitId: {UnitId}",
            Name,
            command.PlayerId,
            command.UnitId);

        return new DecisionResponse(
            Success: true,
            Command: command,
            Reasoning: output.Reasoning,
            ErrorType: null,
            ErrorMessage: null,
            FallbackRequired: false
        );
    }
}
