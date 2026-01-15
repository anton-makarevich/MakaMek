using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Presentation.Models.Logger;

public static partial class GameLoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Attempting to connect to {serverIp}")]
    public static partial void LogAttemptingToConnectToServerip(this ILogger logger, string serverIp);
}