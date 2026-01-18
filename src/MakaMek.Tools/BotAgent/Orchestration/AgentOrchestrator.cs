using BotAgent.Agents;
using BotAgent.Models;

namespace BotAgent.Orchestration;

/// <summary>
/// Agent orchestrator that routes decision requests to appropriate specialized agents.
/// </summary>
public class AgentOrchestrator
{
    private readonly Dictionary<string, ISpecializedAgent> _agents;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        DeploymentAgent deploymentAgent,
        MovementAgent movementAgent,
        WeaponsAttackAgent weaponsAttackAgent,
        EndPhaseAgent endPhaseAgent,
        ILogger<AgentOrchestrator> logger)
    {
        _logger = logger;

        _agents = new Dictionary<string, ISpecializedAgent>(StringComparer.OrdinalIgnoreCase)
        {
            ["Deployment"] = deploymentAgent,
            ["Movement"] = movementAgent,
            ["WeaponsAttack"] = weaponsAttackAgent,
            ["End"] = endPhaseAgent
        };
    }

    /// <summary>
    /// Process a decision request by routing to the appropriate agent.
    /// </summary>
    public async Task<DecisionResponse> ProcessDecisionAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing decision request for player {PlayerId} in phase {Phase}",
            request.PlayerId,
            request.Phase);

        try
        {
            if (!_agents.TryGetValue(request.Phase, out var agent))
            {
                _logger.LogWarning("Unsupported phase: {Phase}", request.Phase);
                return new DecisionResponse(
                    Success: false,
                    Command: null,
                    Reasoning: null,
                    ErrorType: "UNSUPPORTED_PHASE",
                    ErrorMessage: $"Phase '{request.Phase}' is not supported by any agent",
                    FallbackRequired: true
                );
            }

            _logger.LogDebug("Routing to agent: {AgentName}", agent.Name);

            var response = await agent.MakeDecisionAsync(request, cancellationToken);

            _logger.LogInformation(
                "Decision completed - Success: {Success}",
                response.Success);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing decision request");
            return new DecisionResponse(
                Success: false,
                Command: null,
                Reasoning: null,
                ErrorType: "ORCHESTRATOR_ERROR",
                ErrorMessage: $"Orchestrator error: {ex.Message}",
                FallbackRequired: true
            );
        }
    }
}
