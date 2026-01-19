using BotAgent.Services;

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
        McpClientService mcpClient,
        ILogger<WeaponsAttackAgent> logger)
        : base(llmProvider, mcpClient, logger)
    {
    }
}
