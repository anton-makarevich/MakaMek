using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.ResourceProviders;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Services;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// Loads real units from the repository's own <c>data/</c> folder rather than the configured remote
/// source, so a harness test can start a game with no network.
/// </summary>
public static class LocalGameFixture
{
    public static async Task<IReadOnlyList<UnitData>> LoadBundledUnitsAsync(IServiceProvider services)
    {
        var provider = new LocalFolderResourceStreamProvider(UnitsFolder(), "mmux", "bundled-units");
        var caching = new UnitCachingService(
            [provider],
            services.GetRequiredService<ILoggerFactory>());
        var units = await new MmuxUnitsLoader(caching).LoadUnits();
        return units;
    }

    /// <summary>
    /// Walks up from the test binaries to the repository root. Keyed on the data folder itself so a
    /// wrong answer fails here rather than as an empty unit list three layers away.
    /// </summary>
    public static string UnitsFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data", "units", "mechs");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"data/units/mechs not found above {AppContext.BaseDirectory}");
    }
}
