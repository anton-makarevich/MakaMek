namespace Sanet.MakaMek.Services.Avalonia;

/// <summary>
/// Avalonia implementation of IPlatformService that detects mobile platforms (iOS and Android).
/// The result is cached after the first check.
/// </summary>
public class AvaloniaPlatformService : IPlatformService
{
    /// <inheritdoc />
    public bool IsMobile { get; } = OperatingSystem.IsIOS() || OperatingSystem.IsAndroid();
}

