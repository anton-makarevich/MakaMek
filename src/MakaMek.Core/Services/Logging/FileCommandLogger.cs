using System.Collections.Concurrent;
using System.Text;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Localization;

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

        try
        {
            await using var writer = new StreamWriter(_filePath, append: true, Encoding.UTF8);
            var flushCounter = 0;
            foreach (var line in _queue.GetConsumingEnumerable())
            {
                try
                {
                    await writer.WriteAsync(line + Environment.NewLine);
                    if (++flushCounter % 16 == 0)
                        await writer.FlushAsync();
                }
                catch
                {
                    // Swallow transient write failures so the next queued line is still processed
                }
            }
            await writer.FlushAsync();
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // Expected during shutdown: CompleteAdding races with Dispose
        }
    }

    public void Dispose()
    {
        try
        {
            _queue.CompleteAdding();
            _worker.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Swallow to avoid impacting the host
        }
        finally
        {
            _queue.Dispose();
        }
    }
}