# LLM Bot Integration

This document describes the LLM integration for the Integration Bot (Component 2) that enables it to use the BotAgent API (Component 1) for tactical decision making.

## Architecture Overview

The Integration Bot now uses LLM-enabled decision engines that wrap the standard rule-based engines. Each LLM engine:

1. Makes HTTP requests to the BotAgent API for tactical decisions
2. Receives command responses from the LLM agent
3. Executes the returned commands
4. Falls back to the standard rule-based engine if the LLM fails or requests fallback

## Components

### 1. BotAgentClient (`Services/BotAgentClient.cs`)

HTTP client service that handles communication with the BotAgent API:
- Sends `DecisionRequest` with player ID, phase, and MCP server URL
- Receives `DecisionResponse` with command and reasoning
- Handles network errors, timeouts, and deserialization failures
- Returns fallback-required responses on any error

### 2. BotAgentConfiguration (`Configuration/BotAgentConfiguration.cs`)

Configuration class for BotAgent integration:
- `ApiUrl`: BotAgent API endpoint (default: `http://localhost:5244`)
- `McpServerUrl`: MCP server URL for game state queries (default: `http://localhost:5002/mcp`)
- `Timeout`: Request timeout in milliseconds (default: 30000)

### 3. LLM Decision Engines (`DecisionEngines/`)

Four LLM-enabled decision engines, one for each game phase:

#### LlmDeploymentEngine
- Handles Deployment phase decisions
- Executes `DeployUnitCommand`
- Falls back to `DeploymentEngine`

#### LlmMovementEngine
- Handles Movement phase decisions
- Executes `MoveUnitCommand` and `TryStandupCommand`
- Falls back to `MovementEngine`

#### LlmWeaponsEngine
- Handles WeaponsAttack phase decisions
- Executes `WeaponAttackDeclarationCommand` and `WeaponConfigurationCommand`
- Falls back to `WeaponsEngine`

#### LlmEndPhaseEngine
- Handles End phase decisions
- Executes `ShutdownUnitCommand`, `StartupUnitCommand`, and `TurnEndedCommand`
- Falls back to `EndPhaseEngine`

### 4. LlmDecisionEngineProvider (`Services/LlmDecisionEngineProvider.cs`)

Provides LLM-enabled decision engines for each phase. Creates both the LLM engines and their fallback engines.

### 5. LlmBotManager (`Services/LlmBotManager.cs`)

Custom bot manager that uses `LlmDecisionEngineProvider` instead of the standard `DecisionEngineProvider`.

## Configuration

Add the following to `appsettings.json`:

```json
{
  "BotAgent": {
    "ApiUrl": "http://localhost:5244",
    "McpServerUrl": "http://localhost:5002/mcp",
    "Timeout": 30000
  }
}
```

## Dependency Injection

The following services are registered in `DependencyInjection.cs`:

- `BotAgentConfiguration`: Configuration options
- `BotAgentClient`: HTTP client for BotAgent API
- `LlmBotManager`: Bot manager with LLM-enabled engines

## Error Handling and Fallback

The LLM engines implement comprehensive error handling:

1. **Network Errors**: HTTP request failures, timeouts, connection issues
2. **API Errors**: Non-success HTTP status codes
3. **Deserialization Errors**: Invalid JSON responses
4. **Validation Errors**: Invalid command types for the current phase
5. **Fallback Requests**: When `DecisionResponse.FallbackRequired` is true

In all error cases, the engine logs the error and delegates to the standard rule-based engine.

## Logging

Each LLM engine logs:
- Decision requests to BotAgent
- Successful responses with command type and reasoning
- Errors and fallback triggers
- Command execution

Log levels:
- `Information`: Normal operation (requests, responses, command execution)
- `Warning`: Fallback triggers, unexpected command types
- `Error`: Exceptions during decision making

## Future Enhancements

1. **MCP Server Implementation**: The `McpServerUrl` is currently configured but the MCP server for game state queries is not yet implemented
2. **Retry Logic**: Add retry logic for transient network failures
3. **Caching**: Cache LLM responses for similar game states
4. **Metrics**: Track LLM vs fallback usage, response times, success rates
5. **A/B Testing**: Compare LLM vs rule-based performance

