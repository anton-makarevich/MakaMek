namespace Sanet.MakaMek.Assets.Services;

/// <summary>
/// Interface for services that report loading progress
/// </summary>
public interface IProgressReporting
{
    /// <summary>
    /// Raised as assets are loaded, reporting the current load progress
    /// </summary>
    event EventHandler<ResourceLoadProgressEventArgs>? LoadProgress;
}