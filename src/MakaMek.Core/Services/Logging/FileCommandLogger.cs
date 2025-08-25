using System.Collections.Concurrent;
using System.Text;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Services.Logging;

// Resilient, non-blocking logger writing command.Render() lines to a file.
public sealed class FileCommandLogger : ICommandLogger, IDisposable
{
    private readonly ILocalizationService _localizationService;
    private readonly IGame _game;
    private readonly string _filePath;
    private readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>());
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private readonly string _logDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MakaMek", "Commands");

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
            var line = $"{DateTimeOffset.UtcNow:o} | {command.GetType().Name}:{Environment.NewLine}{SafeRender(command)}";
            _queue.Add(line, _cts.Token);
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

        while (!_cts.IsCancellationRequested || _queue.Count > 0)
        {
            string? line = null;
            try
            {
                line = _queue.TryTake(out var item, 100, _cts.Token) ? item : null;
            }
            catch
            {
                // Ignore cancellation/other errors here to keep the loop resilient
            }

            if (line == null)
                continue;

            try
            {
                // Append asynchronously; create the file if it doesn't exist
                await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, Encoding.UTF8, _cts.Token);
            }
            catch
            {
                // Swallows write errors to avoid affecting gameplay
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
            _queue.CompleteAdding();
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Swallow to avoid impacting the host
        }
        finally
        {
            _cts.Dispose();
            _queue.Dispose();
        }
    }
}