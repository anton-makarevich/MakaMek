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

    private async Task SaveFile(string title, string defaultFileName, string defaultExtension, string filterName, Func<Stream, Task> writeAsync)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            DefaultExtension = defaultExtension,
            FileTypeChoices =
            [
                new FilePickerFileType(filterName) { Patterns = [$"*.{defaultExtension}"] }
            ]
        });

        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            await writeAsync(stream);
        }
    }

    public async Task SaveJsonFile(string title, string defaultFileName, string content)
    {
        await SaveFile(title, defaultFileName, "json", "JSON Files", async stream =>
        {
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content);
        });
    }

    public async Task SaveBinaryFile(string title, string defaultFileName, byte[] content, string defaultExtension, string filterName)
    {
        await SaveFile(title, defaultFileName, defaultExtension, filterName, async stream =>
        {
            await stream.WriteAsync(content);
        });
    }

    public async Task<(string? Name, string? Content)> OpenFile(string title)
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
