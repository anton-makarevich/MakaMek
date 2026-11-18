using System.Collections.Concurrent;
using System.Collections.Immutable;
using Sanet.MakaMek.Assets.Models.Terrains;

namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Snapshot of all cached terrain data.
/// </summary>
public sealed class TerrainCacheState : PackageCacheState
{
    /// <summary>
    /// Synchronizes package-merge operations so a duplicate biome is replaced by one complete
    /// package rather than an interleaved mixture from concurrent callers on this same state.
    /// </summary>
    public object SyncRoot { get; } = new();

    public ConcurrentDictionary<string, BiomeManifest> BiomeManifests { get; } = new();
    public ConcurrentDictionary<string, byte[]> ImageCache { get; } = new();
    public ConcurrentDictionary<string, ImmutableSortedSet<int>> VariantCache { get; } = new();
}
