using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Logger;

public static partial class GameLoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Applying {ConfigType} with {HexDirection} when targeting {TargetName}", SkipEnabledCheck = true)]
    public static partial void LogApplyingConfigTypeWithHexDirectionWhenTargetingTargetName(this ILogger logger, WeaponConfigurationType configType, HexDirection hexDirection, string targetName);
    
    [LoggerMessage(LogLevel.Information, "Selected target {TargetName} with score {Score:F1}", SkipEnabledCheck = true)]
    public static partial void LogSelectedTargetWithScore(this ILogger logger, string targetName, double score);
    
    [LoggerMessage(LogLevel.Information, "No units to move for player {PlayerName}, skipping...", SkipEnabledCheck = true)]
    public static partial void LogNoUnitsToMove(this ILogger logger, string playerName);
    
    [LoggerMessage(LogLevel.Information, "Selected {UnitName} with role {Role} and priority {Priority}", SkipEnabledCheck = true)]
    public static partial void LogSelectedUnitWithRoleAndPriority(this ILogger logger, string unitName, UnitTacticalRole role, double priority);
    
    [LoggerMessage(LogLevel.Information, "{UnitName} moving to {Position} using {MovementType} (Offensive: {OffensiveIndex:F1}, Defensive: {DefensiveIndex:F1}, EnemiesInRearArc: {EnemiesInRearArc})", SkipEnabledCheck = true)]
    public static partial void LogUnitMovingToPositionUsingMovementTypeWithOffensiveDefensiveAndEnemiesInRearArc(this ILogger logger, string unitName, HexCoordinates position, MovementType movementType, double offensiveIndex, double defensiveIndex, int enemiesInRearArc);

    [LoggerMessage(LogLevel.Error, "Failed to evaluate path to {Position}: {Message}", SkipEnabledCheck = true)]
    public static partial void LogFailedToEvaluatePath(this ILogger logger, HexCoordinates position, string message, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Bot with PlayerId {PlayerId} not found in game players", SkipEnabledCheck = true)]
    public static partial void LogBotPlayerNotFound(this ILogger logger, Guid playerId);

    [LoggerMessage(LogLevel.Error, "DeploymentEngine error for player {PlayerName}: {Message}", SkipEnabledCheck = true)]
    public static partial void LogDeploymentEngineError(this ILogger logger, string playerName, string message, Exception exception);

    [LoggerMessage(LogLevel.Error, "EndPhaseEngine error for player {PlayerName}: {Message}", SkipEnabledCheck = true)]
    public static partial void LogEndPhaseEngineError(this ILogger logger, string playerName, string message, Exception exception);

    [LoggerMessage(LogLevel.Error, "MovementEngine error for player {PlayerName}: {Message}", SkipEnabledCheck = true)]
    public static partial void LogMovementEngineError(this ILogger logger, string playerName, string message, Exception exception);

    [LoggerMessage(LogLevel.Error, "WeaponsEngine error for player {PlayerName}: {Message}", SkipEnabledCheck = true)]
    public static partial void LogWeaponsEngineError(this ILogger logger, string playerName, string message, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Bot {PlayerName} could not declare the end of the physical attack phase for unit {UnitId}", SkipEnabledCheck = true)]
    public static partial void LogBotCouldNotDeclarePhysicalAttackPhaseEnd(this ILogger logger, string playerName, Guid unitId);
}
