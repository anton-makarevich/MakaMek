namespace Sanet.MakaMek.Services.ResourceProviders;

/// <summary>
/// Resource stream provider that loads assets from a local filesystem directory.
/// Useful for testing new asset files without publishing them to GitHub.
/// </summary>
public class LocalFolderResourceStreamProvider : IResourceStreamProvider
{
    private readonly string _folderPath;
    private readonly string _fileExtension;

    /// <summary>
    /// Initializes a new instance of LocalFolderResourceStreamProvider
    /// </summary>
    /// <param name="folderPath">Absolute path to the local folder containing resource files</param>
    /// <param name="fileExtension">File extension to filter by (e.g., "mmux", "mmtx") without the leading dot</param>
    public LocalFolderResourceStreamProvider(string folderPath, string fileExtension)
    {
        _folderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));
        _fileExtension = fileExtension ?? throw new ArgumentNullException(nameof(fileExtension));
    }

    /// <summary>
    /// Gets all available resource identifiers by scanning the local folder
    /// </summary>
    /// <returns>Collection of full file paths matching the extension</returns>
    public Task<IEnumerable<string>> GetAvailableResourceIds()
    {
        if (!Directory.Exists(_folderPath))
        {
            return Task.FromResult(Enumerable.Empty<string>());
        }

        var files = Directory.GetFiles(_folderPath, $"*.{_fileExtension}", SearchOption.TopDirectoryOnly);
        return Task.FromResult<IEnumerable<string>>(files);
    }

    /// <summary>
    /// Gets a stream for the specified resource identifier (full file path)
    /// </summary>
    /// <param name="resourceId">Full path to the local file</param>
    /// <returns>FileStream for the specified file, or null if not found</returns>
    public Task<Stream?> GetResourceStream(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId) || !File.Exists(resourceId))
        {
            return Task.FromResult<Stream?>(null);
        }

        try
        {
            var stream = new FileStream(resourceId, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<Stream?>(stream);
        }
        catch
        {
            return Task.FromResult<Stream?>(null);
        }
    }
}
