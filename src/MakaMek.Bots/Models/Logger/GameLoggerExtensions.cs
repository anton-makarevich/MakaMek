using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Bots.Models.Logger;

public static partial class GameLoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Applying {ConfigType} with {HexDirection} when targeting {TargetName}")]
    public static partial void LogApplyingConfigTypeWithHexDirectionWhenTargetingTargetName(this ILogger logger, WeaponConfigurationType configType, HexDirection hexDirection, string targetName);
    
    [LoggerMessage(LogLevel.Information, "Selected target {TargetName} with score {Score:F1}")]
    public static partial void LogSelectedTargetWithScore(this ILogger logger, string targetName, double score);
    
    [LoggerMessage(LogLevel.Information, "No units to move for player {PlayerName}, skipping...")]
    public static partial void LogNoUnitsToMove(this ILogger logger, string playerName);
    
    [LoggerMessage(LogLevel.Information, "Selected {UnitName} with role {Role} and priority {Priority}")]
    public static partial void LogSelectedUnitWithRoleAndPriority(this ILogger logger, string unitName, UnitTacticalRole role, double priority);
    
    [LoggerMessage(LogLevel.Information, "{UnitName} moving to {Position} using {MovementType} (Offensive: {OffensiveIndex:F1}, Defensive: {DefensiveIndex:F1}, EnemiesInRearArc: {EnemiesInRearArc})")]
    public static partial void LogUnitMovingToPositionUsingMovementTypeWithOffensiveDefensiveAndEnemiesInRearArc(this ILogger logger, string unitName, HexCoordinates position, MovementType movementType, double offensiveIndex, double defensiveIndex, int enemiesInRearArc);
}