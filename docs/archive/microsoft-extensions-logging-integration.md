# Microsoft.Extensions.Logging Integration Architecture

**Date:** 2026-01-09  
**Status:** Architectural Proposal  
**Related Issues:** #684

## Executive Summary

This document outlines the architectural approach for integrating Microsoft.Extensions.Logging throughout the MakaMek game system. The design enables structured logging with platform-specific providers while maintaining the existing command logging system for game replay functionality.

## Current State Analysis

### Existing Logging Infrastructure

The codebase currently has a specialized command logging system:

- **ICommandLogger**: Interface for logging game commands with `Render()` output
- **ICommandLoggerFactory**: Creates command loggers per game instance
- **Platform Implementations**:
  - `FileCommandLoggerFactory` (Desktop) - writes to `%LocalAppData%/MakaMek/Commands/{gameId}.txt`
  - `ConsoleCommandLoggerFactory` (Android, iOS, Browser) - writes to console
- **Purpose**: Game replay, debugging, command audit trail

This system is **separate from general application logging** and should remain unchanged.

### Dependency Injection Flow

The current DI registration follows this sequence:

1. **Platform Entry Point** (Program.cs, MainActivity.cs, AppDelegate.cs)
   - Calls `.UseDependencyInjection(services => services.RegisterXxxServices())`
   - Platform-specific services registered FIRST

2. **App.axaml.cs.OnFrameworkInitializationCompleted()**
   - Retrieves `IServiceCollection` from Resources (line 34)
   - Calls `services.RegisterServices()` - adds core services
   - Calls `services.RegisterViewModels()` - adds ViewModels
   - Calls `services.BuildServiceProvider()` - creates ServiceProvider

**Key Insight**: Platform-specific code runs BEFORE CoreServices, enabling platform-first logging configuration.

### Game Instance Creation

- **GameFactory**: Creates ServerGame and ClientGame instances using manual factory pattern (no DI)
- **BaseGame**: Abstract base class for both ServerGame and ClientGame - currently has no logger

## Proposed Architecture

### 1. Logger Categories and Naming Conventions

#### Category Strategy

Use **type-based categories** following .NET conventions:

- `ILogger<ServerGame>` - Server-side game logic, phase management, command validation
- `ILogger<ClientGame>` - Client-side game logic, command acknowledgment, state synchronization

**Category Names** (automatically derived from type):
- `Sanet.MakaMek.Core.Models.Game.ServerGame`
- `Sanet.MakaMek.Core.Models.Game.ClientGame`
- `Sanet.MakaMek.Core.Models.Game.GameManager`

#### Structured Logging Approach

Use structured logging with named parameters for rich telemetry:

```csharp
// Good - structured logging
logger.LogInformation("Player {PlayerId} joined game {GameId} with {UnitCount} units", 
    playerId, gameId, units.Count);

// Good - using log scopes for context
using (logger.BeginScope(new Dictionary<string, object>
{
    ["GameId"] = Id,
    ["Turn"] = Turn,
    ["Phase"] = TurnPhase
}))
{
    logger.LogInformation("Processing player action");
}

// Avoid - string interpolation (loses structure)
logger.LogInformation($"Player {playerId} joined game {gameId}");
```

#### Log Level Guidelines

| Level | Usage | Examples |
|-------|-------|----------|
| **Trace** | Detailed flow, calculations | Dice roll results, hit location calculations |
| **Debug** | Game state changes | Phase transitions, initiative order, unit deployment |
| **Information** | Player actions, game events | Player joins, unit moves, weapon attacks |
| **Warning** | Invalid commands, rule violations | Duplicate commands, invalid moves |
| **Error** | Exceptions, recoverable failures | Network errors, command processing failures |
| **Critical** | Game-breaking errors | Unhandled exceptions, data corruption |

### 2. Dependency Injection Registration

#### Platform-Specific Logging Configuration

Each platform configures logging in its `RegisterXxxServices()` method.

**DesktopServices.cs:**
```csharp
public static void RegisterDesktopServices(this IServiceCollection services)
{
    services.AddLogging(builder =>
    {
        builder.AddConsole();  // Base provider
        builder.AddDebug();    // Visual Studio Debug output
        builder.SetMinimumLevel(LogLevel.Information);
        
        // Configure specific category levels
        builder.AddFilter("Sanet.MakaMek.Core.Models.Game", LogLevel.Debug);
        builder.AddFilter("Microsoft", LogLevel.Warning);
    });
    
    // Existing registrations...
}
```

**AndroidServices.cs, IosServices.cs, BrowserServices.cs:**
```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
```

#### CoreServices.cs Changes

**No changes required** - `ILoggerFactory` is automatically registered by `AddLogging()`.

### 3. Logger Injection into Game Instances

#### GameFactory Modifications

Inject `ILoggerFactory` into GameFactory constructor and create loggers for game instances:

```csharp
public class GameFactory : IGameFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public GameFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public ServerGame CreateServerGame(
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IDiceRoller diceRoller,
        IToHitCalculator toHitCalculator,
        IDamageTransferCalculator damageTransferCalculator,
        ICriticalHitsCalculator criticalHitsCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IFallProcessor fallProcessor)
    {
        var logger = _loggerFactory.CreateLogger<ServerGame>();

        return new ServerGame(
            logger,
            rulesProvider,
            mechFactory,
            commandPublisher,
            diceRoller,
            toHitCalculator,
            damageTransferCalculator,
            criticalHitsCalculator,
            pilotingSkillCalculator,
            consciousnessCalculator,
            heatEffectsCalculator,
            fallProcessor);
    }

    public ClientGame CreateClientGame(
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IBattleMapFactory mapFactory,
        IHashService hashService)
    {
        var logger = _loggerFactory.CreateLogger<ClientGame>();

        return new ClientGame(
            logger,
            rulesProvider,
            mechFactory,
            commandPublisher,
            toHitCalculator,
            pilotingSkillCalculator,
            consciousnessCalculator,
            heatEffectsCalculator,
            mapFactory,
            hashService);
    }
}
```

#### BaseGame Modifications

Add protected `ILogger` property to BaseGame:

```csharp
public abstract class BaseGame : IGame
{
    protected ILogger Logger { get; }
    internal readonly ICommandPublisher CommandPublisher;
    // ... other fields

    protected BaseGame(
        ILogger logger,
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator)
    {
        Logger = logger;
        Id = Guid.NewGuid();
        RulesProvider = rulesProvider;
        CommandPublisher = commandPublisher;
        // ... rest of initialization

        Logger.LogDebug("Game instance created with ID {GameId}", Id);
    }
}
```

#### ServerGame Modifications

Update constructor to accept and pass logger to base:

```csharp
public class ServerGame : BaseGame, IDisposable
{
    public ServerGame(
        ILogger<ServerGame> logger,
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IDiceRoller diceRoller,
        IToHitCalculator toHitCalculator,
        IDamageTransferCalculator damageTransferCalculator,
        ICriticalHitsCalculator criticalHitsCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IFallProcessor fallProcessor,
        IPhaseManager? phaseManager = null)
        : base(logger, rulesProvider, mechFactory, commandPublisher,
               toHitCalculator, pilotingSkillCalculator,
               consciousnessCalculator, heatEffectsCalculator)
    {
        DiceRoller = diceRoller;
        DamageTransferCalculator = damageTransferCalculator;
        CriticalHitsCalculator = criticalHitsCalculator;
        FallProcessor = fallProcessor;
        PhaseManager = phaseManager ?? new BattleTechPhaseManager();
        _currentPhase = new StartPhase(this);

        Logger.LogInformation("Server game initialized");
    }
}
```

#### ClientGame Modifications

Update constructor to accept and pass logger to base:

```csharp
public sealed class ClientGame : BaseGame, IDisposable, IClientGame
{
    public ClientGame(
        ILogger<ClientGame> logger,
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IBattleMapFactory mapFactory,
        IHashService hashService,
        int ackTimeoutMilliseconds = 10000)
        : base(logger, rulesProvider, mechFactory, commandPublisher,
               toHitCalculator, pilotingSkillCalculator,
               consciousnessCalculator, heatEffectsCalculator)
    {
        _mapFactory = mapFactory;
        _hashService = hashService;
        _ackTimeout = TimeSpan.FromMilliseconds(ackTimeoutMilliseconds);

        Logger.LogInformation("Client game initialized");
    }
}
```

#### IGame Interface Update

Add `ILogger` property to the interface for consistency:

```csharp
public interface IGame
{
    ILogger Logger { get; }  // Expose logger for external access if needed

    // Existing members...
}
```


### 4. Platform-Specific Logging Provider Extensibility

#### How Platforms Can Add Custom Providers

Each platform can extend logging by adding custom providers in their `RegisterXxxServices()` method.

**Example: Android LogCat Provider**

```csharp
// Custom provider implementation
public class AndroidLogCatLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new AndroidLogCatLogger(categoryName);
    }

    public void Dispose() { }
}

public class AndroidLogCatLogger : ILogger
{
    private readonly string _categoryName;

    public AndroidLogCatLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception exception, Func<TState, Exception, string> formatter)
    {
        var message = formatter(state, exception);
        Android.Util.Log.WriteLine(
            ConvertLogLevel(logLevel),
            _categoryName,
            message);
    }

    private Android.Util.LogPriority ConvertLogLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => Android.Util.LogPriority.Verbose,
            LogLevel.Debug => Android.Util.LogPriority.Debug,
            LogLevel.Information => Android.Util.LogPriority.Info,
            LogLevel.Warning => Android.Util.LogPriority.Warn,
            LogLevel.Error => Android.Util.LogPriority.Error,
            LogLevel.Critical => Android.Util.LogPriority.Assert,
            _ => Android.Util.LogPriority.Info
        };
    }
}

// Registration in AndroidServices.cs
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddProvider(new AndroidLogCatLoggerProvider());
    builder.SetMinimumLevel(LogLevel.Information);
});
```

**Example: iOS OSLog Provider**

```csharp
// Custom provider for iOS unified logging
public class OSLogLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new OSLogLogger(categoryName);
    }

    public void Dispose() { }
}

// Registration in IosServices.cs
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddProvider(new OSLogLoggerProvider());
    builder.SetMinimumLevel(LogLevel.Information);
});
```

**Example: Desktop File-Based Logging**

```csharp
// Using a third-party provider like Serilog
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddDebug();

    // Add Serilog file sink
    var logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.File(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MakaMek", "Logs", "makamek-.log"),
            rollingInterval: RollingInterval.Day,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    builder.AddSerilog(logger);
});
```

### 5. Coexistence with Command Logging

The Microsoft.Extensions.Logging system **complements** the existing command logging system:

| System | Purpose | Output | Scope |
|--------|---------|--------|-------|
| **Command Logging** | Game replay, audit trail | Command.Render() text | Game commands only |
| **Microsoft.Extensions.Logging** | Application diagnostics, debugging | Structured logs | All application components |

**Key Differences:**

1. **Command Logging** (ICommandLogger):
   - Logs game commands for replay functionality
   - Uses `command.Render()` for human-readable output
   - Stored per-game instance (one file per game)
   - Platform-specific: File (Desktop) or Console (Mobile/Browser)
   - Remains unchanged

2. **Microsoft.Extensions.Logging** (ILogger):
   - Logs application events, state changes, errors
   - Uses structured logging with named parameters
   - Centralized logging across all components
   - Platform-specific providers (Console, Debug, File, LogCat, OSLog, etc.)
   - New addition

**Example Usage in ServerGame:**

```csharp
public override void HandleCommand(IGameCommand command)
{
    // Command logging (existing) - for replay
    // Handled by GameManager via ICommandLogger

    // Application logging (new) - for diagnostics
    Logger.LogDebug("Handling command {CommandType} from game {GameOriginId}",
        command.GetType().Name, command.GameOriginId);

    if (command is not IClientCommand and not RequestGameLobbyStatusCommand)
    {
        Logger.LogWarning("Received unexpected command type {CommandType}",
            command.GetType().Name);
        return;
    }

    if (!ShouldHandleCommand(command))
    {
        Logger.LogTrace("Skipping command {CommandType} - should not handle",
            command.GetType().Name);
        return;
    }

    // Check for duplicate commands
    if (command is IClientCommand { IdempotencyKey: not null } clientCommand)
    {
        if (!_processedCommandKeys.TryAdd(clientCommand.IdempotencyKey.Value, 0))
        {
            Logger.LogWarning("Duplicate command detected: {CommandType} with key {IdempotencyKey}",
                command.GetType().Name, clientCommand.IdempotencyKey.Value);

            var errorCommand = new ErrorCommand
            {
                GameOriginId = Id,
                IdempotencyKey = clientCommand.IdempotencyKey.Value,
                ErrorCode = ErrorCode.DuplicateCommand,
                Timestamp = DateTime.UtcNow
            };
            CommandPublisher.PublishCommand(errorCommand);
            return;
        }
    }

    Logger.LogInformation("Successfully processed command {CommandType}",
        command.GetType().Name);
}
```

## Implementation Impact Analysis

### Breaking Changes

The following components require updates due to constructor signature changes:

1. **GameFactory** - Add `ILoggerFactory` constructor parameter
2. **BaseGame** - Add `ILogger` constructor parameter (first parameter)
3. **ServerGame** - Add `ILogger<ServerGame>` constructor parameter (first parameter)
4. **ClientGame** - Add `ILogger<ClientGame>` constructor parameter (first parameter)

### Affected Components

#### Direct Callers (require updates):

1. **All unit tests** - Mock ILogger for game instances

#### DI Registration (require updates):

1. **Platform-specific services** - Add logging configuration

### Migration Checklist

- [ ] Add `Microsoft.Extensions.Logging` NuGet package to MakaMek.Core project
- [ ] Add `Microsoft.Extensions.Logging.Console` NuGet package to MakaMek.Core project
- [ ] Update platform-specific DI files to configure logging
- [ ] Update GameFactory to inject ILoggerFactory
- [ ] Update BaseGame constructor signature
- [ ] Update ServerGame constructor signature
- [ ] Update ClientGame constructor signature
- [ ] Update IClientGame interface (optional)
- [ ] Update all unit tests to provide mock ILogger instances
- [ ] Add logging statements to key game operations
- [ ] Test logging output on all platforms

### Testing Strategy

1. **Unit Tests**:
   - Mock `ILogger<T>` using NSubstitute
   - Verify log calls with `logger.Received().Log(...)`
   - Test that game logic works with null logger (defensive)

## Benefits

1. **Structured Logging**: Rich telemetry with named parameters for better diagnostics
2. **Platform Flexibility**: Each platform can add appropriate logging providers
3. **Industry Standard**: Uses Microsoft.Extensions.Logging, familiar to .NET developers
4. **Minimal Breaking Changes**: Isolated to game instance creation
5. **Coexistence**: Works alongside existing command logging system
6. **Testability**: Easy to mock ILogger in unit tests
7. **Performance**: Logging can be configured per-category and level
8. **Extensibility**: Easy to add new providers (Otel, Application Insights, Seq, etc.)

## Potential Concerns and Mitigations

### Concern 1: Performance Impact

**Mitigation**:
- Use appropriate log levels (avoid Trace in production)
- Configure filters to reduce noise from framework components
- Logging is asynchronous in most providers

### Concern 2: Breaking Changes

**Mitigation**:
- Changes are isolated to game instance creation
- Clear migration path with checklist
- All changes are compile-time errors (easy to find)

### Concern 3: Complexity

**Mitigation**:
- Platform-specific configuration is simple (just AddLogging)
- GameFactory changes are straightforward
- Existing command logging remains unchanged

### Concern 4: Testing Overhead

**Mitigation**:
- Use NSubstitute to mock ILogger
- Create test helper to provide default mock logger
- Logging is optional (game logic doesn't depend on it)

## Future Enhancements

1. **Centralized Logging**: Add Application Insights or Seq for production telemetry
2. **Log Correlation**: Use Activity IDs to correlate logs across distributed components
3. **Performance Metrics**: Add custom metrics providers for game performance tracking
4. **User Analytics**: Integrate with analytics platforms for user behavior tracking
5. **Error Reporting**: Integrate with error tracking services (Sentry, Raygun, etc.)

## Conclusion

This architecture provides a clean, extensible approach to integrating Microsoft.Extensions.Logging throughout the MakaMek game system. The design:

- Leverages the existing DI flow for platform-first configuration
- Minimizes breaking changes by isolating them to game instance creation
- Maintains the existing command logging system for game replay
- Follows .NET logging best practices with structured logging
- Enables platform-specific logging providers through simple extension points

The implementation is straightforward and provides immediate value through better diagnostics and debugging capabilities across all platforms.

