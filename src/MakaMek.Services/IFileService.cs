namespace Sanet.MakaMek.Services;

public interface IFileService
{
    Task SaveTextFile(string title, string defaultFileName, string content);
    Task SaveBinaryFile(string title, string defaultFileName, byte[] content, string defaultExtension, string filterName);
    Task<(string? Name, string? Content)> OpenFile(string title);
}
