using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Generators;

/// <summary>
/// A null-object level provider that always returns level 0 (flat terrain).
/// Used as the default when no hill configuration is provided.
/// </summary>
public class FlatLevelProvider : ILevelProvider
{
    /// <inheritdoc/>
    public int GetLevel(HexCoordinates coordinates) => 0;
}