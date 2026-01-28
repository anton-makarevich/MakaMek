using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Tools.BotAgent.Services.LlmProviders;

namespace Sanet.MakaMek.Tools.BotAgent.Models.Agents;

/// <summary>
/// Weapons attack phase agent - selects targets and weapon configurations.
/// </summary>
public class WeaponsAttackAgent : BaseAgent
{
    public override string Name => "WeaponsAttackAgent";

    protected override string SystemPrompt => """
        You are a BattleTech tactical AI specializing in weapons targeting. Your goal is to maximize damage output while managing heat and ammunition.

        CRITICAL REQUIREMENTS:
        - You can ONLY attack with units that have NOT declared weapon attacks yet (HasDeclaredWeaponAttack = false)
        - If ALL units have declared attacks or no units exist, you MUST return an error response
        - Each decision is for ONE unit only
        - USE get_combat_options tool to get target evaluations with weapon configurations

        TACTICAL PRINCIPLES:
        - Prioritize targets based on:
          * Damaged units (lower armor/structure) - easier to destroy
          * High-value targets (heavier mechs, shutdown units)
          * Hit probability vs damage potential
        - Heat Management:
          * Monitor current heat and projected heat after firing
          * Avoid shutdown risk (heat 30+)
          * Consider heat dissipation capacity
        - Ammunition Conservation:
          * Prefer energy weapons when ammo is low
          * Don't waste ammo on low-probability shots
        - Weapon Configuration:
          * Apply torso rotation (TorsoRotation) if needed to bring weapons to bear
          * Configuration must be applied BEFORE declaring attack

        DECISION PROCESS:
        1. Check if ANY units haven't declared attacks - if not, return error response
        2. Identify which unit to attack with:
           - Use the unit specified in "ATTACK WITH UNIT:" if present
           - Otherwise, select the any unit that hasn't declared attack
        3. Call get_combat_options(unitId) to get target evaluations
        4. Analyze options:
           - Review each target's evaluations
           - Consider configuration requirements (torso rotation angle)
           - Evaluate heat impact and shutdown risk
           - Check ammunition availability
        5. Select best option based on score and tactical situation
        6. If configuration needed and not yet applied:
           - Call make_weapon_configuration_decision with configuration details. that is the final decision.
        7. If configuration was already applied or not needed:
           - Select weapons to fire (balance damage, heat, ammo)
           - Call make_weapon_attack_decision with selected weapons
        8. If no viable targets or choosing to skip:
           - Call make_weapon_attack_decision with empty weapon list

        WEAPON SELECTION STRATEGY:
        - Start with highest hit probability weapons
        - Add weapons until heat threshold reached (avoid shutdown)
        - Skip ammo weapons with low remaining shots unless high hit probability
        - Energy weapons are "free" (no ammo cost) but generate heat

        VALIDATION CHECKLIST (before responding):
        ✓ Is there at least one unit that hasn't declared attack?
        ✓ Is unitId a valid GUID from a unit in YOUR UNITS?
        ✓ Have you called get_combat_options to evaluate targets?
        ✓ If configuring: Is configuration type valid? Is value correct?
        ✓ If attacking: Are weapon names from get_combat_options results?
        ✓ Does reasoning explain the tactical choice clearly?
        """;

    public WeaponsAttackAgent(
        ILlmProvider llmProvider,
        ILogger<WeaponsAttackAgent> logger)
        : base(llmProvider, logger)
    {
    }

    protected override List<AITool> GetLocalTools()
    {
        return [
            AIFunctionFactory.Create(MakeWeaponConfigurationDecision, "make_weapon_configuration_decision"),
            AIFunctionFactory.Create(MakeWeaponAttackDecision, "make_weapon_attack_decision")
        ];
    }

    [Description("Execute a weapon configuration decision (e.g., torso rotation)")]
    private string MakeWeaponConfigurationDecision(
        [Description("Unit GUID")] Guid unitId,
        [Description("Configuration type: TorsoRotation, ArmsFlip, None")] string configurationType,
        [Description("Configuration value (e.g., hex direction 0-5 for torso rotation)")] int value,
        [Description("Tactical reasoning")] string reasoning)
    {
        if (!Enum.TryParse<WeaponConfigurationType>(configurationType, true, out var configType))
            throw new ArgumentException($"Invalid configuration type: {configurationType}");

        var command = new WeaponConfigurationCommand
        {
            UnitId = unitId,
            Configuration = new WeaponConfiguration
            {
                Type = configType,
                Value = value
            },
            GameOriginId = Guid.Empty,
            PlayerId = Guid.Empty // Will be set later
        };

        PendingDecision = new ValueTuple<IClientCommand, string>(command, reasoning);
        return JsonSerializer.Serialize(new { success = true, message = "Weapon configuration decision recorded" });
    }

    [Description("Execute a weapon attack decision with selected weapons")]
    private string MakeWeaponAttackDecision(
        [Description("Unit GUID")] Guid unitId,
        [Description("Target GUID")] Guid targetId,
        [Description("List of weapon names to fire (empty list to skip attack)")] List<string> weaponNames,
        [Description("Tactical reasoning")] string reasoning)
    {
        if (LastRequest == null)
        {
            throw new InvalidOperationException("INVALID_REQUEST: No request data available");
        }
        
        // Find the unit in request
        var unit = LastRequest.ControlledUnits.FirstOrDefault(u => u.Id == unitId);
        if (unit.Equipment == null)
        {
            throw new InvalidOperationException($"INVALID_UNIT: Unit {unitId} not found in controlled units");
        }

        // Resolve weapon names to ComponentData from Equipment
        var weaponTargets = new List<WeaponTargetData>();
        foreach (var weaponName in weaponNames)
        {
            // Find weapon in equipment by name
            var weapon = unit.Equipment.FirstOrDefault(c =>
                c.Name?.Equals(weaponName, StringComparison.OrdinalIgnoreCase) == true);

            if (weapon != null)
            {
                weaponTargets.Add(new WeaponTargetData
                {
                    Weapon = weapon,
                    TargetId = targetId,
                    IsPrimaryTarget = true
                });
            }
        }
        
        var command = new WeaponAttackDeclarationCommand
        {
            UnitId = unitId,
            WeaponTargets = weaponTargets,
            GameOriginId = Guid.Empty,
            PlayerId = Guid.Empty // Will be set later
        };

        // Store weapon names and target for later resolution
        PendingDecision = new ValueTuple<IClientCommand, string>(command, reasoning);

        return JsonSerializer.Serialize(new { success = true, message = "Weapon attack decision recorded" });
    }

    /// <summary>
    /// Build user prompt with game context for weapons attack decisions.
    /// </summary>
    protected override string BuildUserPrompt(DecisionRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Make a weapons attack decision.");
        sb.AppendLine();

        // Add controlled units information

        sb.AppendLine("YOUR UNITS:");
        foreach (var unit in request.ControlledUnits)
        {
            sb.AppendLine($"- {unit.Model} ({unit.Mass} tons)");
            if (unit.Id.HasValue)
                sb.AppendLine($"  ID: {unit.Id.Value}");

            if (unit.State.Position != null)
                sb.AppendLine(
                    $"  Position: Q={unit.State.Position.Coordinates.Q}, R={unit.State.Position.Coordinates.R}, Facing={unit.State.Position.Facing}");
            sb.AppendLine($"  Has declared attack: {unit.State.DeclaredWeaponTargets!= null}");
        }

        sb.AppendLine();

        // Add enemy units information
        sb.AppendLine("ENEMY UNITS:");
        foreach (var enemy in request.EnemyUnits)
        {
            sb.AppendLine($"- {enemy.Model} ({enemy.Mass} tons)");
            if (enemy.State.Position != null)
                sb.AppendLine($"  Position: Q={enemy.State.Position.Coordinates.Q}, R={enemy.State.Position.Coordinates.R}");
        }

        sb.AppendLine();

        // Add a specific unit to attack with if specified
        if (!request.UnitToAct.HasValue) return sb.ToString();
        sb.AppendLine($"ATTACK WITH UNIT: {request.UnitToAct.Value}");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Make the actual weapons attack decision using the provided agent.
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

            // Validate tools
            if (!availableTools.Contains("get_combat_options"))
            {
                throw new InvalidOperationException("Required weapons attack tools are not available");
            }

            // Build the user prompt
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

    /// <summary>
    /// Create a decision response from the agent run response.
    /// </summary>
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
            // Handle WeaponAttackDeclarationCommand to resolve weapon names
            WeaponAttackDeclarationCommand attackCommand => attackCommand with { PlayerId = request.PlayerId },
            WeaponConfigurationCommand configCommand => configCommand with { PlayerId = request.PlayerId },
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
