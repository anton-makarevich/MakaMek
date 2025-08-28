using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Services.Logging.Factories;

public sealed class ConsoleCommandLoggerFactory : ICommandLoggerFactory
{
    public ICommandLogger CreateLogger(ILocalizationService localizationService, IGame game)
    {
        return new ConsoleCommandLogger(localizationService, game);
    }
}
