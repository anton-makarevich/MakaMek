using BotAgent.Services.LlmProviders;
using Microsoft.Agents.AI;

namespace BotAgent.Models.Agents;

/// <summary>
/// Weapons attack phase agent - selects targets and weapon configurations.
/// </summary>
public class WeaponsAttackAgent : BaseAgent
{
    public override string Name => "WeaponsAttackAgent";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in weapons targeting.
        Consider torso rotation if targets are out of arc (WeaponConfigurationCommand).
        Select best targets and fire weapons (WeaponAttackDeclarationCommand).
        Consider:
        - Target priority (damaged units, high-value targets)
        - Hit probability vs potential damage
        - Heat generation and shutdown risk
        - Ammunition conservation
        - Weapon configurations (torso twist, aimed shots)
        
        Analyze available targets and select the optimal weapon configuration.
        """;

    public WeaponsAttackAgent(
        ILlmProvider llmProvider,
        ILogger<WeaponsAttackAgent> logger)
        : base(llmProvider, logger)
    {
    }

    /// <summary>
    /// Build user prompt with game context for weapons attack decisions.
    /// </summary>
    protected override string BuildUserPrompt(DecisionRequest request) => 
        throw new NotImplementedException("BuildUserPrompt not yet implemented for this agent");

    /// <summary>
    /// Make the actual weapons attack decision using the provided agent.
    /// </summary>
    protected override Task<DecisionResponse> GetAgentDecision(
        ChatClientAgent agent, 
        DecisionRequest request, 
        string[] availableTools,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(CreateErrorResponse("NOT_IMPLEMENTED", "WeaponsAttackAgent not yet implemented"));
    }
}
