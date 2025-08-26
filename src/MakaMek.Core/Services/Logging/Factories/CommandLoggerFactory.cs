using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Services.Logging.Factories;

public class CommandLoggerFactory : ICommandLoggerFactory
{
    public ICommandLogger CreateFileLogger(ILocalizationService localizationService, IGame game)
    {
        return new FileCommandLogger(localizationService, game);
    }
}