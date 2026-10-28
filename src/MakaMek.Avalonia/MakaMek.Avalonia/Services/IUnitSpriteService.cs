using System.Threading.Tasks;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>Provides decoded MMUX v2 sprite animation frames for unit controls.</summary>
public interface IUnitSpriteService
{
    /// <summary>
    /// Gets the decoded sprite set for a model, or <c>null</c> when the package has no (valid)
    /// animation data. Results, including "no animation", are cached per model.
    /// </summary>
    Task<UnitSpriteSet?> GetSpriteSet(string model);
}