namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Base class for the immutable-by-publication snapshot of all cached data.
/// A new instance is built completely and then published via a single volatile
/// write to the owning <see cref="PackageCacheCore{TState}"/>, so readers either
/// observe the previous complete cache or the new complete cache, never a
/// cleared or partially rebuilt state.
/// </summary>
public abstract class PackageCacheState
{
    public volatile bool IsInitialized;
}