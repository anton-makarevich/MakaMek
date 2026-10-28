namespace Sanet.MakaMek.Presentation;

/// <summary>
/// Temporary switches for features that are implemented but must stay off until their content ships.
/// </summary>
public static class FeatureFlags
{
    /// <summary>
    /// Enables movement playback and unit sprite animation. It currently depends on MMUX v2 packages,
    /// which are not ready yet, so with this off the board keeps moving units instantly and never
    /// publishes playback commands.
    /// </summary>
    public static bool IsUnitAnimationEnabled { get; set; }
}
