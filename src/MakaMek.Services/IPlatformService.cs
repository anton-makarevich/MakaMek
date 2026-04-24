namespace Sanet.MakaMek.Services;

/// <summary>
/// Service for detecting whether the application is running on a mobile platform.
/// </summary>
public interface IPlatformService
{
    /// <summary>
    /// Gets a value indicating whether the application is running on a mobile device (iOS or Android).
    /// </summary>
    bool IsMobile { get; }

    /// <summary>
    /// Gets a value indicating whether the application is running in a browser (WASM).
    /// </summary>
    bool IsBrowser { get; }
}

