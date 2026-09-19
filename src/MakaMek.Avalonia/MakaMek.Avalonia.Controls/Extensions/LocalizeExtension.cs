using Avalonia;
using Avalonia.Markup.Xaml;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Avalonia.Controls.Extensions;

/// <summary>
/// Markup extension that resolves a localization key to a localized string.
/// Usage in XAML: Text="{extensions:Localize 'Some_Key'}"
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    /// <summary>
    /// Resource key under which App.OnFrameworkInitializationCompleted stores the
    /// DI-resolved <see cref="ILocalizationService"/> in <see cref="Application.Resources"/>.
    /// </summary>
    public const string LocalizationServiceResourceKey = "LocalizationServiceResource";

    public string Key { get; set; } = string.Empty;

    public LocalizeExtension() { }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var localizationService =
            Application.Current?.Resources[LocalizationServiceResourceKey] as ILocalizationService;

        // Non-bindable targets (e.g. MultiBinding.StringFormat) need a plain string value,
        // not a binding — provide it eagerly, without change notification.
        var valueTarget = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        if (localizationService is null ||
            valueTarget is not { TargetProperty: AvaloniaProperty })
        {
            // Graceful fallback: show the key itself (e.g. at design time)
            return localizationService?.GetString(Key) ?? Key;
        }

        IObservable<string> localizedText = Observable.Create<string>(observer =>
        {
            void Handler(object? sender, EventArgs args) => observer.OnNext(localizationService.GetString(Key));
            observer.OnNext(localizationService.GetString(Key));
            localizationService.LanguageChanged += Handler;
            return Disposable.Create(() => localizationService.LanguageChanged -= Handler);
        });

        // Bind to the observable so the target property re-resolves when the language changes
        return localizedText.ToBinding();
    }
}