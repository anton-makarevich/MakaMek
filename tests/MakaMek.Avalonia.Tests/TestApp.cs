using Avalonia;
using Avalonia.Markup.Xaml;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.MakaMek.Localization;

namespace MakaMek.Avalonia.Tests;

public partial class TestApp : Application
{
    private static readonly IAvaloniaResourcesLocator ResourcesLocator = Substitute.For<IAvaloniaResourcesLocator>();
    private static readonly ILocalizationService LocalizationService = Substitute.For<ILocalizationService>();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RegisterConverterResources();
    }

    // XAML views used in headless tests resolve DI-injected converters via StaticResource;
    // register them like the real app does (constructor injection, static lookup order by key).
    private void RegisterConverterResources()
    {
        Resources["HubStatusBackgroundConverter"] = new Sanet.MakaMek.Avalonia.Converters.HubStatusBackgroundConverter(ResourcesLocator);
        Resources["HubStatusTextConverter"] = new Sanet.MakaMek.Avalonia.Converters.HubStatusTextConverter(LocalizationService);
        Resources["ConnectionStatusBackgroundConverter"] = new Sanet.MakaMek.Avalonia.Converters.ConnectionStatusBackgroundConverter(ResourcesLocator);
        Resources["ConnectionStatusTextConverter"] = new Sanet.MakaMek.Avalonia.Converters.ConnectionStatusTextConverter(LocalizationService);
        Resources["ComponentStatusBackgroundConverter"] = new Sanet.MakaMek.Avalonia.Converters.ComponentStatusBackgroundConverter(ResourcesLocator);
        Resources["SelectedItemToBrushConverter"] = new Sanet.MakaMek.Avalonia.Converters.SelectedItemToBrushConverter(ResourcesLocator);
        Resources["EventTypeToBackgroundConverter"] = new Sanet.MakaMek.Avalonia.Converters.EventTypeToBackgroundConverter(ResourcesLocator);
        Resources["ConsciousnessColorConverter"] = new Sanet.MakaMek.Avalonia.Converters.ConsciousnessColorConverter(ResourcesLocator);
        Resources["ConsciousnessStatusConverter"] = new Sanet.MakaMek.Avalonia.Converters.ConsciousnessStatusConverter(LocalizationService);
        Resources["ModifierToTextConverter"] = new Sanet.MakaMek.Avalonia.Converters.ModifierToTextConverter(LocalizationService);
        Resources["SegmentEventToTextConverter"] = new Sanet.MakaMek.Avalonia.Converters.SegmentEventToTextConverter(LocalizationService);
        Resources["MovementBreakdownConverter"] = new Sanet.MakaMek.Avalonia.Converters.MovementBreakdownConverter(LocalizationService);
    }
}
