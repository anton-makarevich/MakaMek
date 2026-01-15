using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Presentation.Models.Logger;

public static partial class GameLoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Attempted to connect to {serverIp}")]
    public static partial void LogAttemptedToConnectToServerIp(this ILogger logger, string serverIp);
}