using BotAgent.Models;
using BotAgent.Services;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;

namespace BotAgent.Agents;

/// <summary>
/// Weapons attack phase agent - selects targets and weapon configurations.
/// </summary>
public class WeaponsAttackAgent : BaseAgent
{
    public override string Name => "WeaponsAttackAgent";
    public override string Description => "Specialist in weapons targeting and attack optimization";

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

    protected override IClientCommand ParseDecision(string responseText, DecisionRequest request)
    {
        // TODO: Parse actual JSON response from LLM
        // TODO: Check if we need torso rotation (WeaponConfigurationCommand) or attack (WeaponAttackDeclarationCommand)
        
        // Placeholder: Default to WeaponAttackDeclarationCommand
        return new WeaponAttackDeclarationCommand
        {
            PlayerId = request.PlayerId,
            UnitId = Guid.Empty, // From context
            GameOriginId = Guid.Empty, // From context
            WeaponTargets = new List<WeaponTargetData>(),
            IdempotencyKey = Guid.NewGuid()
        };
    }
}
