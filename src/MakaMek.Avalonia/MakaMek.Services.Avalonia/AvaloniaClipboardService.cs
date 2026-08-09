using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace Sanet.MakaMek.Services.Avalonia;

public class AvaloniaClipboardService : IClipboardService
{
    private TopLevel? GetTopLevel()
    {
        return Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => TopLevel.GetTopLevel(desktop.MainWindow),
            ISingleViewApplicationLifetime singleView => TopLevel.GetTopLevel(singleView.MainView),
            _ => null
        };
    }

    public async Task SetTextAsync(string text)
    {
        var topLevel = GetTopLevel();
        if (topLevel?.Clipboard is null) return;

        try
        {
            await topLevel.Clipboard.SetTextAsync(text);
        }
        catch
        {
            // Ignore clipboard failures (unsupported platform, permission denied).
        }
    }
}
