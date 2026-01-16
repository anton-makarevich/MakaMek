using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Bots.Models.Logger;

public static partial class GameLoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Applying {ConfigType} with {HexDirection} when targeting {TargetName}")]
    public static partial void LogApplyingConfigTypeWithHexDirectionWhenTargetingTargetName(this ILogger logger, WeaponConfigurationType configType, HexDirection hexDirection, string targetName);
    
    [LoggerMessage(LogLevel.Information, "Selected target {TargetName} with score {Score:F1}")]
    public static partial void LogSelectedTargetWithScore(this ILogger logger, string targetName, double score);
}