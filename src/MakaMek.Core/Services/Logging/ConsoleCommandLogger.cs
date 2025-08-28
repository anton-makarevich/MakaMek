using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Services.Logging;

// Logger writing command.Render() lines to console.
// Designed for WASM/browser environments where a file system is not available.
public sealed class ConsoleCommandLogger : ICommandLogger
{
    private readonly ILocalizationService _localizationService;
    private readonly IGame _game;

    public ConsoleCommandLogger(ILocalizationService localizationService, IGame game)
    {
        _localizationService = localizationService;
        _game = game;
    }

    public void Log(IGameCommand command)
    {
        if (command.GameOriginId != _game.Id) return;
        var line =
            $"{DateTimeOffset.UtcNow:o} | {command.GetType().Name}:{Environment.NewLine}{SafeRender(command)}{Environment.NewLine}";
        Console.WriteLine(line);
    }

    private string SafeRender(IGameCommand command)
    {
        try
        {
            return command.Render(_localizationService, _game);
        }
        catch
        {
            return "<Render() failed>";
        }
    }
    
    public void Dispose()
    {
    }
}
