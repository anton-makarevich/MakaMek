# LLM Deployment Agent - Detailed Design Document

**Version:** 1.0
**Date:** 2026-01-19
**Status:** Design Specification
**Related:** `docs/architecture/llm-bot-system-design.md`

---

## Executive Summary

This document provides a comprehensive design for implementing complete LLM decision-making functionality for the Deployment phase in the MakaMek bot system. The design addresses identified gaps in the current `DeploymentAgent` implementation and provides detailed specifications for:

- Enhanced game state communication via `DecisionRequest`
- MCP tools for deployment zone queries
- JSON parsing and command validation
- Enhanced system prompts with BattleTech tactical knowledge
- Complete integration flow with error handling

**Goal**: Enable the `DeploymentAgent` to make informed, tactical deployment decisions using LLM reasoning while maintaining seamless integration with the existing `LlmDeploymentEngine` and fallback mechanisms.

---

## 1. Current State Analysis

### 1.1 Human Player Deployment (DeploymentState.cs)

**Three-step workflow**:
1. **SelectingUnit**: Player selects which unit to deploy
2. **SelectingHex**: Player selects deployment position (edge hex, unoccupied)
3. **SelectingDirection**: Player selects facing direction (0-5, representing Top, TopRight, BottomRight, Bottom, BottomLeft, TopLeft)

**Command Structure**:
```csharp
DeployUnitCommand {
    UnitId: Guid,
    Position: HexCoordinateData { Q: int, R: int },
    Direction: int (0-5),
    GameOriginId: Guid,
    PlayerId: Guid,
    IdempotencyKey: Guid?
}
```

**Validation**:
- Hex must not be occupied by another unit
- Position must be in deployment zone 
- Direction must be 0-5

### 1.2 Rule-Based Bot Deployment (DeploymentEngine.cs)

**Decision Logic**:
1. Find first undeployed unit from player's units
2. Get occupied hexes (all deployed units across all players)
3. Get deployment area using `BattleMap.GetEdgeHexCoordinates()`
4. Filter valid hexes: `deploymentArea - occupiedHexes`
5. Randomly select a valid hex
6. Calculate strategic facing:
   - If enemies deployed: face toward nearest enemy
   - If no enemies: face toward map center
   - Use `HexCoordinates.LineTo()` to get direction
7. Create and send `DeployUnitCommand`

**Key Methods**:
- `GetDeploymentArea()`: Returns edge hexes as `HashSet<HexCoordinates>`
- `GetValidDeploymentHexes()`: Filters unoccupied hexes
- `GetDeploymentDirection()`: Calculates facing based on target

### 1.3 Current LLM System Architecture

**Two-Container Architecture**:
- **Component 1 (BotAgent)**: ASP.NET Core Web API hosting specialized agents
- **Component 2 (BotContainer)**: Integration Bot with MCP Server and ClientGame

**Current DecisionRequest**:
```csharp
public record DecisionRequest(
    Guid PlayerId,
    string Phase,
    string McpServerUrl,
    int Timeout = 30000
);
```

**Current DecisionResponse**:
```csharp
public record DecisionResponse(
    bool Success,
    IGameCommand? Command,
    string? Reasoning,
    string? ErrorType,
    string? ErrorMessage,
    bool FallbackRequired
);
```

**Current DeploymentAgent Issues**:
- `ParseDecision()` returns dummy command with `Guid.Empty` values
- No actual JSON parsing implementation
- No MCP tool integration
- Generic system prompt without tactical guidance
- No game state information in request

---

## 2. Identified Gaps and Requirements

### 2.1 Critical Gaps

1. **Insufficient Game State in DecisionRequest**
   - No unit information (controlled units, enemy units)
   - No deployment zone information
   - No map context
   - Agent cannot make informed decisions without this data

2. **Missing MCP Tools**
   - `McpClientService` has placeholder methods
   - No actual MCP protocol implementation
   - No tools registered with `ChatClientAgent`

3. **No JSON Parsing Logic**
   - `ParseDecision()` is a stub
   - No validation of LLM output
   - No error handling for malformed responses

4. **Generic System Prompt**
   - Lacks BattleTech tactical knowledge
   - No output format specification
   - No guidance on decision criteria

5. **No Validation Layer**
   - No checks for valid unit IDs
   - No position validation
   - No direction range validation

### 2.2 Requirements for Informed Decisions

The agent needs access to:
- **Controlled Units**: List of bot's units with deployment status
- **Enemy Units**: List of enemy units with positions (if deployed)
- **Valid Deployment Zones**: Edge hexes that are unoccupied
- **Unit Selection**: Which unit to deploy (or let agent choose)

---

## 3. Design Decisions

### 3.1 DecisionRequest Enhancement


### 3.2 MCP Tools Design

**Approach**: Implement a core MCP tool for deployment decisions

**Tool: get_deployment_zones**
```json
{
  "name": "get_deployment_zones",
  "description": "Get valid deployment hexes (unoccupied edge hexes)",
  "inputSchema": {
    "type": "object",
    "properties": {},
    "required": []
  }
}
```

**Response**:
```json
{
  "validHexes": [
    {"q": 1, "r": 1},
    {"q": 1, "r": 2},
    ...
  ],
  "mapWidth": 10,
  "mapHeight": 10,
  "mapCenter": {"q": 5, "r": 5}
}
```

**Implementation**:
- Call `BattleMap.GetEdgeHexCoordinates()`
- Return list of valid positions with map metadata

```

**Tool Registration**:
Tools should be registered with `ChatClientAgent` in `DeploymentAgent` constructor:
```csharp
Agent = new ChatClientAgent(
    chatClient: llmProvider.GetChatClient(),
    instructions: SystemPrompt,
    tools: new[] {
        GetDeploymentZonesTool()
    }
);
```

### 3.3 JSON Parsing Strategy

**Approach**: Hybrid - structured prompt with JSON response parsing

**LLM Output Format**:
```json
{
  "unitId": "guid-string",
  "position": {"q": 1, "r": 1},
  "direction": 0,
  "reasoning": "Deploy heavy mech at northern edge facing toward map center..."
}
```

**Parsing Implementation**:
```csharp
protected override IClientCommand ParseDecision(string responseText, DecisionRequest request)
{
    try
    {
        // Extract JSON from response (may be wrapped in markdown code blocks)
        var jsonText = ExtractJson(responseText);

        // Parse JSON
        var decision = JsonSerializer.Deserialize<DeploymentDecision>(jsonText);

        // Validate
        if (decision == null)
            throw new JsonException("Failed to deserialize deployment decision");

        if (!Guid.TryParse(decision.UnitId, out var unitId))
            throw new JsonException($"Invalid unitId: {decision.UnitId}");

        if (decision.Direction < 0 || decision.Direction > 5)
            throw new JsonException($"Invalid direction: {decision.Direction}");

        // Create command
        return new DeployUnitCommand
        {
            PlayerId = request.PlayerId,
            UnitId = unitId,
            GameOriginId = Guid.Empty, // Will be set by ClientGame
            Position = new HexCoordinateData(decision.Position.Q, decision.Position.R),
            Direction = decision.Direction,
            IdempotencyKey = Guid.NewGuid()
        };
    }
    catch (JsonException ex)
    {
        Logger.LogError(ex, "Failed to parse LLM response as JSON");
        throw new InvalidOperationException("INVALID_LLM_RESPONSE", ex);
    }
}
```

**Helper Methods**:
```csharp
private string ExtractJson(string text)
{
    // Handle markdown code blocks: ```json ... ```
    var jsonMatch = Regex.Match(text, @"```(?:json)?\s*(\{.*?\})\s*```", RegexOptions.Singleline);
    if (jsonMatch.Success)
        return jsonMatch.Groups[1].Value;

    // Try to find raw JSON object
    var rawMatch = Regex.Match(text, @"\{.*\}", RegexOptions.Singleline);
    if (rawMatch.Success)
        return rawMatch.Value;

    throw new JsonException("No JSON found in response");
}
```

**Supporting Types**:

`DeploymentDecision` -> use DeployUnitCommand

`PositionData` -> use HexCoordinateData

### 3.4 Enhanced System Prompt

**Approach**: Comprehensive prompt with tactical guidance and output specification

**Enhanced SystemPrompt**:
```csharp
protected override string SystemPrompt => """
    You are a BattleTech tactical AI specializing in unit deployment. Your role is to select
    optimal deployment positions and facing directions for your units.

    TACTICAL PRINCIPLES:
    - Deploy valid deployment zones 
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
    - Valid deployment zones: use get_deployment_zones tool

    DECISION PROCESS:
    1. Identify which unit to deploy (first undeployed unit if not specified)
    2. Analyze valid deployment positions
    3. Consider enemy positions and map center
    4. Select position that maximizes tactical advantage
    5. Calculate optimal facing direction

    OUTPUT FORMAT:
    Respond with JSON only (no additional text):
    { 
      "command":{
        "unitId": "guid-string",
        "position": {"q": int, "r": int},
        "direction": int (0-5)
      },
      "reasoning": "brief explanation"
    }

    Direction (facing) values:
    0 = Top, 1 = TopRight, 2 = BottomRight, 3 = Bottom, 4 = BottomLeft, 5 = TopLeft
    """;
```

**Key Elements**:
1. **Role Definition**: Clear statement of agent's purpose
2. **Tactical Principles**: BattleTech-specific deployment strategies
3. **Available Information**: What data the agent has access to
4. **Decision Process**: Step-by-step guidance
5. **Output Format**: Exact JSON structure expected
6. **Direction Mapping**: Clear explanation of direction values

---

## 4. Implementation Specifications

### 4.1 DecisionRequest.cs Changes

**File**: `src/MakaMek.Tools/BotAgent.Models/DecisionRequest.cs`

**Changes**:
```csharp
using Sanet.MakaMek.Core.Data.Units;

namespace BotAgent.Models;

/// <summary>
/// Request from Integration Bot to LLM Agent for tactical decision.
/// </summary>
/// <param name="PlayerId">The ID of the player (bot) requesting the decision.</param>
/// <param name="Phase">The current game phase (Deployment, Movement, WeaponsAttack, End).</param>
/// <param name="McpServerUrl">The URL of the Integration Bot's MCP Server for game state queries.</param>
/// <param name="Timeout">Request timeout in milliseconds (default: 30000).</param>
/// <param name="ControlledUnits">Bot's units with full state (optional, deployment-specific).</param>
/// <param name="EnemyUnits">Enemy units with positions (optional, deployment-specific).</param>
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
```

### 4.2 DeploymentAgent.cs Changes

**File**: `src/MakaMek.Tools/BotAgent/Agents/DeploymentAgent.cs`

**Complete Implementation**:
```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using BotAgent.Models;
using BotAgent.Services;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Map;

namespace BotAgent.Agents;

/// <summary>
/// Deployment phase agent - selects optimal deployment position and facing for units.
/// </summary>
public class DeploymentAgent : BaseAgent
{
    public override string Name => "DeploymentAgent";
    public override string Description => "Specialist in unit deployment and initial positioning";

    protected override string SystemPrompt => """
        // Enhanced system prompt from section 3.4 above 
        """;

    public DeploymentAgent(
        ILlmProvider llmProvider,
        McpClientService mcpClient,
        ILogger<DeploymentAgent> logger)
        : base(llmProvider, mcpClient, logger)
    {
    }

    protected override IClientCommand ParseDecision(string responseText, DecisionRequest request)
    {
        try
        {
            // Extract JSON from response (may be wrapped in markdown code blocks)
            var jsonText = ExtractJson(responseText);

            // Parse JSON
            var decision = JsonSerializer.Deserialize<DeployUnitCommand>(jsonText);

            // Validate
            if (decision == null)
                throw new JsonException("Failed to deserialize deployment decision");

            if (!Guid.TryParse(decision.UnitId, out var unitId))
                throw new JsonException($"Invalid unitId: {decision.UnitId}");

            if (decision.Direction < 0 || decision.Direction > 5)
                throw new JsonException($"Invalid direction: {decision.Direction}");

            // Create command
            return new DeployUnitCommand
            {
                PlayerId = request.PlayerId,
                UnitId = unitId,
                GameOriginId = Guid.Empty, // Will be set by ClientGame
                Position = new HexCoordinateData(decision.Position.Q, decision.Position.R),
                Direction = decision.Direction,
                IdempotencyKey = Guid.NewGuid()
            };
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "Failed to parse LLM response as JSON");
            throw new InvalidOperationException("INVALID_LLM_RESPONSE", ex);
        }
    }

    private string ExtractJson(string text)
    {
        // Handle markdown code blocks: ```json ... ```
        var jsonMatch = Regex.Match(text, @"```(?:json)?\s*(\{.*?\})\s*```", RegexOptions.Singleline);
        if (jsonMatch.Success)
            return jsonMatch.Groups[1].Value;

        // Try to find raw JSON object
        var rawMatch = Regex.Match(text, @"\{.*\}", RegexOptions.Singleline);
        if (rawMatch.Success)
            return rawMatch.Value;

        throw new JsonException("No JSON found in response");
    }
}
```

### 4.3 LlmDeploymentEngine.cs Changes

**File**: `src/MakaMek.Tools/BotContainer/Models/DecisionEngines/LlmDeploymentEngine.cs`

**Changes to populate DecisionRequest**:

Modify the base class `LlmDecisionEngine<T>` to include controlled and enemy units (from _clientGame) and unit to act (from turnState). this is the same for all the phases

### 4.4 BaseAgent.cs Enhancement

**File**: `src/MakaMek.Tools/BotAgent/Agents/BaseAgent.cs`

**Changes to build user prompt with game context**:

```csharp
public async Task<DecisionResponse> MakeDecisionAsync(
    DecisionRequest request,
    CancellationToken cancellationToken = default)
{
    try
    {
        Logger.LogInformation("{AgentName} making decision for player {PlayerId}", Name, request.PlayerId);

        // Build user prompt with game context from DecisionRequest
        var userPrompt = BuildUserPrompt(request);

        // Run agent
        var response = await Agent.RunAsync(userPrompt, cancellationToken: cancellationToken);

        var responseText = response.ToString(); // Or extract content properly

        // Parse response to command
        var command = ParseDecision(responseText, request);

        return CreateSuccessResponse(command, responseText);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error in {AgentName} decision making", Name);
        return CreateErrorResponse("AGENT_ERROR", ex.Message);
    }
}

/// <summary>
/// Build user prompt with game context. Can be overridden by specialized agents.
/// </summary>
protected virtual string BuildUserPrompt(DecisionRequest request)
{
    var sb = new StringBuilder();
    sb.AppendLine($"Make a tactical decision for player {request.PlayerId} in phase {request.Phase}.");
    sb.AppendLine();

    // Add controlled units information
    if (request.ControlledUnits != null && request.ControlledUnits.Count > 0)
    {
        sb.AppendLine("YOUR UNITS:");
        foreach (var unit in request.ControlledUnits)
        {
            var deployStatus = unit.Id.HasValue && request.ControlledUnits.Any(u => u.Id == unit.Id)
                ? "DEPLOYED" : "UNDEPLOYED";
            sb.AppendLine($"- {unit.Model} ({unit.Mass} tons) - {deployStatus}");
            if (unit.Id.HasValue)
                sb.AppendLine($"  ID: {unit.Id.Value}");
        }
        sb.AppendLine();
    }

    // Add enemy units information
    if (request.EnemyUnits != null && request.EnemyUnits.Count > 0)
    {
        sb.AppendLine("ENEMY UNITS:");
        foreach (var enemy in request.EnemyUnits)
        {
            sb.AppendLine($"- {enemy.Model} ({enemy.Mass} tons)");
            // Position info would be included if available in UnitData
        }
        sb.AppendLine();
    }

    // Add specific unit to deploy if specified
    if (request.UnitToAct.HasValue)
    {
        sb.AppendLine($"DEPLOY UNIT: {request.UnitToAct.Value}");
        sb.AppendLine();
    }

    sb.AppendLine("Select the best deployment position and facing direction.");

    return sb.ToString();
}
```

---

## 5. Integration Flow

### 5.1 Complete Decision Flow

```mermaid
sequenceDiagram
    participant BM as BotManager
    participant LDE as LlmDeploymentEngine
    participant BAC as BotAgentClient
    participant API as BotAgent API
    participant AO as AgentOrchestrator
    participant DA as DeploymentAgent
    participant LLM as LLM Provider
    participant CG as ClientGame

    BM->>LDE: MakeDecision(player, turnState)
    LDE->>LDE: CreateDecisionRequest(playerId, mcpServerUrl)
    Note over LDE: Populate ControlledUnits,<br/>EnemyUnits, UnitToAct
    LDE->>BAC: RequestDecisionAsync(request)
    BAC->>API: POST /api/decision
    API->>AO: ProcessDecisionAsync(request)
    AO->>DA: MakeDecisionAsync(request)
    DA->>DA: BuildUserPrompt(request)
    DA->>LLM: RunAsync(userPrompt)
    LLM-->>DA: JSON response
    DA->>DA: ParseDecision(responseText)
    DA->>DA: Validate command
    DA-->>AO: DecisionResponse(success, command, reasoning)
    AO-->>API: DecisionResponse
    API-->>BAC: HTTP 200 + DecisionResponse
    BAC-->>LDE: DecisionResponse
    LDE->>LDE: ExecuteCommandAsync(command)
    LDE->>CG: DeployUnit(deployCommand)

    alt Error occurs
        DA-->>AO: DecisionResponse(error, fallbackRequired)
        AO-->>API: DecisionResponse
        API-->>BAC: HTTP 200 + DecisionResponse
        BAC-->>LDE: DecisionResponse
        LDE->>FallbackEngine: MakeDecision(player)
    end
```

### 5.2 Key Integration Points

1. **LlmDeploymentEngine.CreateDecisionRequest()**
   - Queries `ClientGame.Players` for controlled and enemy units
   - Converts units to `UnitData` using `unit.ToData()`
   - Constructs `DecisionRequest` with game state

2. **DeploymentAgent.BuildUserPrompt()**
   - Formats game state from `DecisionRequest` into natural language
   - Includes unit lists
   - Provides clear instructions for LLM

3. **DeploymentAgent.ParseDecision()**
   - Extracts JSON from LLM response (handles markdown wrapping)
   - Deserializes to `DeployUnitCommand` record
   - Validates unit ID, position, direction
   - Creates `DeployUnitCommand` with validated data

4. **LlmDeploymentEngine.ExecuteCommandAsync()**
   - Receives command from BotAgentClient
   - Validates command type
   - Sends to `ClientGame.DeployUnit()`
   - Falls back to `DeploymentEngine` on error

---

## 6. Error Handling Strategy

### 6.1 Error Types

| Error Type | Cause | Fallback Required | Handling |
|------------|-------|-------------------|----------|
| `INVALID_LLM_RESPONSE` | JSON parsing failure | Yes | Log error, use fallback engine |
| `INVALID_UNIT` | Unit ID not found or invalid GUID | Yes | Log error, use fallback engine |
| `INVALID_POSITION` | Position out of bounds or occupied | Yes | Log error, use fallback engine |
| `INVALID_DIRECTION` | Direction not in range 0-5 | Yes | Log error, use fallback engine |
| `MCP_ERROR` | MCP tool call failure | Yes | Log error, use fallback engine |
| `LLM_TIMEOUT` | LLM request timeout | Yes | Log error, use fallback engine |
| `AGENT_ERROR` | Unexpected agent error | Yes | Log error, use fallback engine |

### 6.2 Error Handling Implementation

**In DeploymentAgent.ParseDecision()**:
```csharp
try
{
    // Parsing and validation logic
}
catch (JsonException ex)
{
    Logger.LogError(ex, "Failed to parse LLM response as JSON");
    throw new InvalidOperationException("INVALID_LLM_RESPONSE", ex);
}
catch (Exception ex)
{
    Logger.LogError(ex, "Unexpected error parsing decision");
    throw new InvalidOperationException("AGENT_ERROR", ex);
}
```

**In BaseAgent.MakeDecisionAsync()**:
```csharp
catch (InvalidOperationException ex) when (ex.Message.StartsWith("INVALID_"))
{
    return CreateErrorResponse(ex.Message, ex.InnerException?.Message ?? ex.Message);
}
catch (Exception ex)
{
    Logger.LogError(ex, "Error in {AgentName} decision making", Name);
    return CreateErrorResponse("AGENT_ERROR", ex.Message);
}
```

**In LlmDeploymentEngine.MakeDecision()**:
```csharp
catch (Exception ex)
{
    Logger.LogError(
        ex,
        "{EngineType}: Error making decision for player {PlayerName}. Using fallback engine.",
        GetType().Name,
        player.Name);

    await FallbackEngine.MakeDecision(player, turnState);
}
```

### 6.3 Validation Checklist

Before creating `DeployUnitCommand`, validate:
- ✅ Unit ID is valid GUID
- ✅ Unit exists in player's units
- ✅ Unit is not already deployed
- ✅ Position is not occupied
- ✅ Direction is 0-5

**Note**: For MVP, basic validation (GUID, direction range) is sufficient. 
---

## 7. Testing Considerations

### 7.1 Manual Testing Checklist

- [ ] Deploy first unit with no enemies → faces map center
- [ ] Deploy second unit with enemy deployed → faces enemy
- [ ] Deploy heavy mech → selects forward position
- [ ] Deploy light mech → selects flanking position
- [ ] Invalid LLM response → falls back to rule-based engine
- [ ] LLM timeout → falls back to rule-based engine
- [ ] Multiple bots deploying → no conflicts

---

## 8. Future Enhancements

### 8.1 Phase 2 Improvements

1. **Full MCP Tool Integration**
   - Implement actual MCP protocol calls
   - Register tools with ChatClientAgent
   - LLM calls tools directly instead of manual prompt building

2. **Structured Output**
   - Use Microsoft Agent Framework's structured output feature
   - Eliminate JSON parsing errors

---

## 9. Summary and Next Steps

### 9.1 Summary

This design document provides a complete specification for implementing LLM-based deployment decisions in the MakaMek bot system. Key achievements:

✅ **Enhanced DecisionRequest** with game state (ControlledUnits, EnemyUnits, UnitToAct)
✅ **MCP Tools Design** for deployment zones, unit info, and enemy positions
✅ **JSON Parsing Strategy** with validation and error handling
✅ **Enhanced SystemPrompt** with BattleTech tactical knowledge
✅ **Complete Integration Flow** from LlmDeploymentEngine to ClientGame
✅ **Comprehensive Error Handling** with fallback to rule-based engine
✅ **Testing Strategy** for unit, integration, and manual testing

### 9.2 Implementation Checklist

- [ ] Update `DecisionRequest.cs` with additional fields
- [ ] Implement `DeploymentAgent.ParseDecision()` with JSON parsing
- [ ] Implement `DeploymentAgent.ExtractJson()` helper
- [ ] Update `DeploymentAgent.SystemPrompt` with enhanced prompt
- [ ] Implement `DeploymentAgent.BuildUserPrompt()` override
- [ ] Update `LlmDeploymentEngine.CreateDecisionRequest()` to populate game state
- [ ] Add `CreateDecisionRequest()` virtual method to `LlmDecisionEngine<T>` base class
- [ ] Update `BaseAgent.BuildUserPrompt()` virtual method

### 9.3 Success Criteria

The implementation is successful when:
1. ✅ DeploymentAgent receives game state in DecisionRequest
2. ✅ DeploymentAgent generates valid DeployUnitCommand from LLM response
3. ✅ Unit is deployed at correct position with correct facing
4. ✅ LLM reasoning is logged and observable
5. ✅ Errors trigger fallback to DeploymentEngine
6. ✅ All unit tests pass
7. ✅ Integration tests pass
8. ✅ Manual testing shows tactical deployment decisions

---

**Document Version**: 1.0
**Last Updated**: 2026-01-19
**Status**: Ready for Implementation


