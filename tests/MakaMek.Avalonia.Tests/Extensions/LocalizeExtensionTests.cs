using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using NSubstitute;
using Shouldly;
using Sanet.MakaMek.Avalonia.Controls.Extensions;
using Sanet.MakaMek.Localization;

namespace MakaMek.Avalonia.Tests.Extensions;

public class LocalizeExtensionTests
{
    private static readonly HeadlessUnitTestSession Session =
        HeadlessUnitTestSession.StartNew(typeof(TestApp));

    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly LocalizeExtension _sut = new();

    private static IServiceProvider CreateBindableTargetProvider()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        var valueTarget = Substitute.For<IProvideValueTarget>();
        valueTarget.TargetProperty.Returns(TextBlock.TextProperty);
        serviceProvider.GetService(typeof(IProvideValueTarget)).Returns(valueTarget);
        return serviceProvider;
    }

    private void Run(Action action)
    {
        Session.Dispatch(() => action(), CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void Constructor_WithNoParameters_SetsEmptyKey()
    {
        var extension = new LocalizeExtension();

        extension.Key.ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_WithKeyParameter_SetsKey()
    {
        const string expectedKey = "Test_Key";

        var extension = new LocalizeExtension(expectedKey);

        extension.Key.ShouldBe(expectedKey);
    }

    [Fact]
    public void ProvideValue_WhenTargetIsNotAnAvaloniaProperty_ReturnsLocalizedString()
    {
        Run(() =>
        {
            const string key = "Test_Key";
            const string localizedText = "Localized Text";
            _sut.Key = key;
            _localizationService.GetString(key).Returns(localizedText);
            Application.Current!.Resources[LocalizeExtension.LocalizationServiceResourceKey] = _localizationService;

            // e.g. MultiBinding.StringFormat — not an AvaloniaProperty, must get a plain string
            var result = _sut.ProvideValue(Substitute.For<IServiceProvider>());

            result.ShouldBeOfType<string>();
            result.ShouldBe(localizedText);
        });
    }

    [Fact]
    public void ProvideValue_WhenNoServiceInApplicationResources_ReturnsKey()
    {
        Run(() =>
        {
            _sut.Key = "Test_Key";
            Application.Current?.Resources.Remove(LocalizeExtension.LocalizationServiceResourceKey);

            var result = _sut.ProvideValue(CreateBindableTargetProvider());

            result.ShouldBe("Test_Key");
        });
    }

    [Fact]
    public void ProvideValue_WhenServiceAvailableAsResource_ReturnsReactiveBinding()
    {
        Run(() =>
        {
            const string key = "Test_Key";
            const string localizedText = "Localized Text";
            _sut.Key = key;
            _localizationService.ClearReceivedCalls();
            _localizationService.GetString(key).Returns(localizedText);
            Application.Current!.Resources[LocalizeExtension.LocalizationServiceResourceKey] = _localizationService;

            var result = _sut.ProvideValue(CreateBindableTargetProvider());

            result.ShouldBeAssignableTo<BindingBase>();
            var observable = GetObservable((BindingBase)result!);
            var results = new List<object?>();
            using (observable.Subscribe(results.Add))
            {
                results.Count.ShouldBe(1);
                results[0].ShouldBe((object?)localizedText);
                _localizationService.Received(1).GetString(key);
            }
        });
    }

    [Fact]
    public void ProvideValue_WhenLanguageChanged_ObservableEmitsNewValue()
    {
        Run(() =>
        {
            const string key = "Test_Key";
            const string localizedText = "Localized Text";
            const string newText = "New Text";
            _sut.Key = key;
            _localizationService.ClearReceivedCalls();
            _localizationService.GetString(key).Returns(localizedText);
            Application.Current!.Resources[LocalizeExtension.LocalizationServiceResourceKey] = _localizationService;

            var result = _sut.ProvideValue(CreateBindableTargetProvider());
            result.ShouldBeAssignableTo<BindingBase>();
            var observable = GetObservable((BindingBase)result!);

            var results = new List<object?>();
            using (observable.Subscribe(results.Add))
            {
                _localizationService.GetString(key).Returns(newText);
                RaiseLanguageChanged();

                results.Count.ShouldBe(2);
                results[0].ShouldBe((object?)localizedText);
                results[1].ShouldBe((object?)newText);
                _localizationService.Received(2).GetString(key);
            }
        });
    }

    [Fact]
    public void ProvideValue_WithEmptyKey_ReturnsEmptyString()
    {
        Run(() =>
        {
            _sut.Key = string.Empty;
            _localizationService.ClearReceivedCalls();
            _localizationService.GetString(string.Empty).Returns(string.Empty);
            Application.Current!.Resources[LocalizeExtension.LocalizationServiceResourceKey] = _localizationService;

            var result = _sut.ProvideValue(CreateBindableTargetProvider());

            result.ShouldBeAssignableTo<BindingBase>();
            var observable = GetObservable((BindingBase)result!);
            var results = new List<object?>();
            using (observable.Subscribe(results.Add))
            {
                results.Count.ShouldBe(1);
                results[0].ShouldBe((object?)string.Empty);
            }
        });
    }

    [Fact]
    public void ProvideValue_WhenSubscriptionDisposed_UnsubscribesFromLanguageChanged()
    {
        Run(() =>
        {
            _sut.Key = "Test_Key";
            Application.Current!.Resources[LocalizeExtension.LocalizationServiceResourceKey] = _localizationService;

            var observable = GetObservable((BindingBase)_sut.ProvideValue(CreateBindableTargetProvider()));
            using (observable.Subscribe(_ => { })) { }

            // Raising the event after disposal must not throw (subscriber was detached)
            Should.NotThrow(RaiseLanguageChanged);
        });
    }

    private static IObservable<object?> GetObservable(BindingBase binding)
    {
        var target = new TextBlock();
        target.Bind(TextBlock.TextProperty, binding);
        return target.GetObservable(TextBlock.TextProperty);
    }

    private void RaiseLanguageChanged()
    {
        _localizationService.LanguageChanged +=
            Raise.EventWith(new object(), EventArgs.Empty);
    }
}
