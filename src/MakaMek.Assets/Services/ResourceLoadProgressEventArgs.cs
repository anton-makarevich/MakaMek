namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Event arguments describing the number of assets loaded so far during a load operation
/// </summary>
public class ResourceLoadProgressEventArgs : EventArgs
{
    public ResourceLoadProgressEventArgs(int loadedCount, int totalCount)
    {
        LoadedCount = loadedCount;
        TotalCount = totalCount;
    }

    /// <summary>
    /// Gets the number of assets that have been loaded so far
    /// </summary>
    public int LoadedCount { get; }

    /// <summary>
    /// Gets the total number of assets to load
    /// </summary>
    public int TotalCount { get; }
}