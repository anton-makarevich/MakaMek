using Sanet.MakaMek.Core.Data.Game.Commands;

namespace Sanet.MakaMek.Core.Services.Logging;

public interface ICommandLogger: IDisposable
{
    // Logs a game command. Implementations should handle their own exceptions.
    void Log(IGameCommand command);
}