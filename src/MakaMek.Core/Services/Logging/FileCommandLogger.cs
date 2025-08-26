using System.Collections.Concurrent;
using System.Text;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Services.Logging;

// Resilient, non-blocking logger writing command.Render() lines to a file.
public sealed class FileCommandLogger : ICommandLogger
{
    private readonly ILocalizationService _localizationService;
    private readonly IGame _game;
    private readonly string _filePath;
    private readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>());
    private readonly Task _worker;
    private readonly string _logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MakaMek", "Commands");
    private bool _isDisposed;

    public FileCommandLogger(ILocalizationService localizationService, IGame game)
    {
        _localizationService = localizationService;
        _game = game;
        _filePath = Path.Combine(_logDir, $"{game.Id}.txt");
        _worker = Task.Run(WriterLoop);
    }

    public void Log(IGameCommand command)
    {
        try
        {
            if (command.GameOriginId != _game.Id) return;
            var line = $"{DateTimeOffset.UtcNow:o} | {command.GetType().Name}:{Environment.NewLine}{SafeRender(command)}{Environment.NewLine}";
            _queue.Add(line);
        }
        catch
        {
            // Swallow to avoid impacting normal operations
        }
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

    private async Task WriterLoop()
    {
        // Ensure directory exists
        try
        {
            if (!string.IsNullOrEmpty(_logDir))
                Directory.CreateDirectory(_logDir);
        }
        catch
        {
            // If directory creation fails, we still keep draining queue and swallowing errors
        }

        while (!_isDisposed)
        {
            try
            {
                if (_queue.Count < 1) continue;
                var line = _queue.TryTake(out var item, 100) ? item : null;
                if (line == null)
                    continue;
                // Append asynchronously; create the file if it doesn't exist
                await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Ignore cancellation/other errors here to keep the loop resilient
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _queue.CompleteAdding();
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Swallow to avoid impacting the host
        }
        finally
        {
            _queue.Dispose();
            _isDisposed = true;
        }
    }
}