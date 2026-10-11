using Microsoft.Extensions.DependencyInjection;
using Sanet.Localization;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.TestHelpers;

internal static class TestLocalizationService
{
    public static ILocalizationService Create()
    {
        var services = new ServiceCollection();
        services.AddLocalization(typeof(LocalizationResourcesAnchor).Assembly,
            LocalizationResourcesAnchor.ResourceBaseName);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ILocalizationService>();
    }
}