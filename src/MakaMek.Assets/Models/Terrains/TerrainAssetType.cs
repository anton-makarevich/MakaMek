namespace Sanet.MakaMek.Assets.Models.Terrains;

/// <summary>
/// Defines the types of terrain assets available in MMTX packages
/// </summary>
public enum TerrainAssetType
{
    /// <summary>
    /// Base terrain image for a hex (the underlying terrain type)
    /// </summary>
    Base,
    
    /// <summary>
    /// Terrain overlay images (woods, water, rough, etc.)
    /// </summary>
    Overlay,
    
    /// <summary>
    /// Top edge effect (cliff dropping away from viewer)
    /// </summary>
    EdgeTop,
    
    /// <summary>
    /// Bottom edge effect (cliff rising toward viewer)
    /// </summary>
    EdgeBottom,

    /// <summary>
    /// Water terrain bitmask texture (from terrains/water/ folder).
    /// Files are named using the 6-bit binary representation of the canonical bitmask, e.g. 000001.png.
    /// </summary>
    Water
}
