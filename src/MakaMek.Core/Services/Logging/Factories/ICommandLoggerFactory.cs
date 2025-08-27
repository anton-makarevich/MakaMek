using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Services.Logging.Factories;

public interface ICommandLoggerFactory
{
    ICommandLogger CreateLogger(ILocalizationService localizationService, IGame game);
}