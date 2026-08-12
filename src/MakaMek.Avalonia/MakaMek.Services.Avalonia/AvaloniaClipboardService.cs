using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Services.Avalonia;

public class AvaloniaClipboardService : IClipboardService
{
    private readonly ILogger<AvaloniaClipboardService> _logger;

    public AvaloniaClipboardService(ILogger<AvaloniaClipboardService> logger)
    {
        _logger = logger;
    }

    private TopLevel? GetTopLevel()
    {
        return Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => TopLevel.GetTopLevel(desktop.MainWindow),
            ISingleViewApplicationLifetime singleView => TopLevel.GetTopLevel(singleView.MainView),
            _ => null
        };
    }

    public async Task<bool> SetText(string text)
    {
        var topLevel = GetTopLevel();
        if (topLevel?.Clipboard is null) return false;

        try
        {
            using var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.CreateText(text));
            await topLevel.Clipboard.SetDataAsync(transfer);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy text to the clipboard");
            return false;
        }
    }

    public async Task<string?> GetText()
    {
        var topLevel = GetTopLevel();
        if (topLevel?.Clipboard is null) return null;

        try
        {
            using var data = await topLevel.Clipboard.TryGetDataAsync();
            if (data is null) return null;
            return await data.TryGetTextAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read text from the clipboard");
            return null;
        }
    }
}
