using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// Modal dialog view model for adding a new asset provider.
/// Collects the provider type, asset type and URL or path and returns them
/// as an <see cref="AddProviderResult"/> to the caller.
/// </summary>
public class AddProviderViewModel : BaseViewModel, IResultProvider<AddProviderResult?>
{
    private readonly TaskCompletionSource<AddProviderResult?> _resultTaskCompletionSource = new();
    private readonly ILocalizationService _localizationService;

    public AddProviderViewModel(
        IReadOnlyList<ProviderType> providerTypes,
        IReadOnlyList<AssetType> assetTypes,
        ILocalizationService localizationService)
    {
        ProviderTypes = providerTypes;
        AssetTypes = assetTypes;
        _localizationService = localizationService;
        SelectedProviderType = ProviderType.Bucket;
        SelectedAssetType = AssetType.Units;
        ConfirmCommand = new AsyncCommand(Confirm);
        CancelCommand = new AsyncCommand(Cancel);
    }

    public IReadOnlyList<ProviderType> ProviderTypes { get; }

    public IReadOnlyList<AssetType> AssetTypes { get; }

    public ProviderType SelectedProviderType
    {
        get;
        set => SetProperty(ref field, value);
    }

    public AssetType SelectedAssetType
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string UrlOrPath
    {
        get;
        set
        {
            SetProperty(ref field, value);
            ValidationMessage = string.Empty;
        }
    } = string.Empty;

    public string ValidationMessage
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(HasValidationMessage));
        }
    } = string.Empty;

    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

    public ICommand ConfirmCommand { get; }

    public ICommand CancelCommand { get; }

    public Task<AddProviderResult?> GetResultAsync() => _resultTaskCompletionSource.Task;

    private Task Confirm()
    {
        if (string.IsNullOrWhiteSpace(UrlOrPath))
        {
            ValidationMessage = _localizationService.GetString("Settings_Data_Providers_UrlOrPathRequired");
            return Task.CompletedTask;
        }

        _resultTaskCompletionSource.TrySetResult(new AddProviderResult
        {
            ProviderType = SelectedProviderType,
            AssetType = SelectedAssetType,
            UrlOrPath = UrlOrPath.Trim()
        });
        return Task.CompletedTask;
    }

    private Task Cancel()
    {
        _resultTaskCompletionSource.TrySetResult(null);
        return Task.CompletedTask;
    }
}
