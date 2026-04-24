      namespace Sanet.MakaMek.Bots.Utilities;

/// <summary>
/// Provides runtime platform detection and default configuration values
/// for cooperative processing strategies.
/// </summary>
public static class PlatformDetection
{
    /// <summary>
    /// Returns true when running in a WASM/browser environment.
    /// </summary>
    public static bool IsBrowser => OperatingSystem.IsBrowser();

    /// <summary>
    /// Whether to use parallel processing. Disabled on WASM/browser
    /// because threading is not supported.
    /// </summary>
    public static bool UseParallelProcessing => !IsBrowser;

    /// <summary>
    /// Default chunk size for batched async processing.
    /// </summary>
    public static int DefaultChunkSize => 15;

    /// <summary>
    /// Default number of iterations between cooperative yield points.
    /// </summary>
    public static int DefaultYieldFrequency => 5;
}
