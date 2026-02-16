namespace Sanet.MakaMek.Services;

public interface IFileService
{
    Task SaveFileAsync(string title, string defaultFileName, string content);
    Task<string?> OpenFileAsync(string title);
}
