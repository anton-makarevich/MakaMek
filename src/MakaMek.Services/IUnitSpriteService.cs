namespace Sanet.MakaMek.Services;

/// <summary>
/// Provides unit sprite animation frames for the board. The sprite set itself is platform specific
/// (see <see cref="IUnitSpriteService{TSpriteSet}"/>), so presentation code only holds this
/// non-generic contract and hands it to the platform layer.
/// </summary>
public interface IUnitSpriteService
{
    /// <summary>
    /// Drops every decoded sprite set so atlases are re-read from the providers on next use.
    /// </summary>
    void ClearCache();
}

public interface IUnitSpriteService<TSpriteSet> : IUnitSpriteService
{
    /// <summary>
    /// Gets the decoded sprite set for a model, or <c>null</c> when the package has no (valid)
    /// animation data. Results, including "no animation", are cached per model.
    /// </summary>
    Task<TSpriteSet?> GetSpriteSet(string model);
}
