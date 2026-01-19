using Sanet.MakaMek.Core.Data.Units;

namespace BotAgent.Models;

/// <summary>
/// Request from Integration Bot to LLM Agent for tactical decision.
/// </summary>
/// <param name="PlayerId">The ID of the player (bot) requesting the decision.</param>
/// <param name="Phase">The current game phase (Deployment, Movement, WeaponsAttack, End).</param>
/// <param name="McpServerUrl">The URL of the Integration Bot's MCP Server for game state queries.</param>
/// <param name="Timeout">Request timeout in milliseconds (default: 30000).</param>
/// <param name="ControlledUnits">Bot's units with full state.</param>
/// <param name="EnemyUnits">Enemy units with positions.</param>
/// <param name="UnitToAct">Specific unit to deploy (optional, if null agent chooses).</param>
public record DecisionRequest(
    Guid PlayerId,
    string Phase,
    string McpServerUrl,
    int Timeout = 30000,
    List<UnitData>? ControlledUnits = null,
    List<UnitData>? EnemyUnits = null,
    Guid? UnitToAct = null
);
