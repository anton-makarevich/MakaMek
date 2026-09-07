namespace Sanet.MakaMek.Assets.Services;

using System.Collections.Concurrent;

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

    /// <summary>
    /// Attributes each cached resource key (unit model, biome id) to the provider id that loaded
    /// it. Uses merged-cache (overwrite) semantics: when a provider lower in the list overwrites
    /// an earlier duplicate, the ownership entry is overwritten too.
    /// </summary>
    public readonly ConcurrentDictionary<string, string> ResourceOwnership = new();
}