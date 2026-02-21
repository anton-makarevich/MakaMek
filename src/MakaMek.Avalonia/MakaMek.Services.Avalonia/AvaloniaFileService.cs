using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Sanet.MakaMek.Services.Avalonia;

public class AvaloniaFileService : IFileService
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

    public async Task SaveFile(string title, string defaultFileName, string content)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            DefaultExtension = "json",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON Files") { Patterns = ["*.json"] }
            ]
        });

        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content);
        }
    }

    public async Task<(string? Name, string? Content)> OpenFileAsync(string title)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return (null, null);

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON Files") { Patterns = ["*.json"] }
            ]
        });

        if (files.Count >= 1)
        {
            var file = files[0];
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            return (file.Name, content);
        }

        return (null, null);
    }
}
