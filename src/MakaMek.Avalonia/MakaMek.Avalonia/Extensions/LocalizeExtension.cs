using System;
using Avalonia.Markup.Xaml;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Avalonia.Extensions;

/// <summary>
/// Markup extension that resolves a localization key to a localized string.
/// Usage in XAML: Text="{extensions:Localize 'Some_Key'}"
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    private static ILocalizationService? _localizationService;

    public static void Initialize(ILocalizationService localization)
    {
        _localizationService = localization;
    }

    public string Key { get; set; } = string.Empty;

    public LocalizeExtension() { }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return _localizationService == null ? Key : // Graceful fallback: show the key itself
            _localizationService.GetString(Key);
    }
}