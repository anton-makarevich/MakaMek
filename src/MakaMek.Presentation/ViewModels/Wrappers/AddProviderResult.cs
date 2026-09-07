using Sanet.MakaMek.Assets.Configuration;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// Result of the "Add Provider" dialog, or null if it was cancelled.
/// </summary>
public class AddProviderResult
{
    public ProviderType ProviderType { get; init; }

    public AssetType AssetType { get; init; }

    public string UrlOrPath { get; init; } = string.Empty;
}
