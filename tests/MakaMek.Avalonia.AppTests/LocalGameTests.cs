using Microsoft.Extensions.DependencyInjection;
using global::Avalonia;
using global::Avalonia.Headless;
using Sanet.MakaMek.Avalonia;
using Shouldly;

namespace MakaMek.Avalonia.AppTests;

/// <summary>Can the harness assemble a real game without a network? Everything else depends on it.</summary>
public class LocalGameTests
{
    [Fact]
    public Task BundledUnits_LoadFromTheRepositoryDataFolder() => HarnessSession.Run(async () =>
    {
        var services = ((App)Application.Current!).ServiceProvider!;

        var units = await LocalGameFixture.LoadBundledUnitsAsync(services);

        units.Count.ShouldBeGreaterThan(0, $"no units loaded from {LocalGameFixture.UnitsFolder()}");
        units.ShouldContain(unit => !string.IsNullOrWhiteSpace(unit.Chassis));
    });

}
