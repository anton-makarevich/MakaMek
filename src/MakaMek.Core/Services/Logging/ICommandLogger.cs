using Sanet.MakaMek.Core.Data.Game.Commands;

namespace Sanet.MakaMek.Core.Services.Logging;

public interface ICommandLogger
{
    // Logs a game command. Implementations should handle their own exceptions.
    void Log(IGameCommand command);
}