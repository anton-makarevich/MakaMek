namespace Sanet.MakaMek.Services;

public interface IFileService
{
    Task SaveFile(string title, string defaultFileName, string content);
    Task<(string? Name, string? Content)> OpenFileAsync(string title);
}
