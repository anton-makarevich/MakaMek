namespace Sanet.MakaMek.Assets.Data.Terrain;

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
    EdgeBottom
}
