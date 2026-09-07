using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// Modal dialog view model for adding a new relay hub.
/// Collects the hub name, base URL and API key and returns them
/// as an <see cref="AddHubResult"/> to the caller.
/// </summary>
public class AddHubViewModel : BaseViewModel, IResultProvider<AddHubResult?>
{
    private readonly TaskCompletionSource<AddHubResult?> _resultTaskCompletionSource = new();
    private readonly ILocalizationService _localizationService;

    public AddHubViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        ConfirmCommand = new AsyncCommand(Confirm);
        CancelCommand = new AsyncCommand(Cancel);
    }

    public string Name
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string BaseUrl
    {
        get;
        set
        {
            SetProperty(ref field, value);
            ValidationMessage = string.Empty;
        }
    } = string.Empty;

    public string ApiKey
    {
        get;
        set => SetProperty(ref field, value);
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

    public Task<AddHubResult?> GetResultAsync() => _resultTaskCompletionSource.Task;

    private async Task Confirm()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            ValidationMessage = _localizationService.GetString("Settings_Hub_UrlRequired");
            return;
        }

        _resultTaskCompletionSource.TrySetResult(new AddHubResult
        {
            Name = string.IsNullOrWhiteSpace(Name) ? string.Empty : Name.Trim(),
            BaseUrl = BaseUrl.Trim(),
            ApiKey = ApiKey
        });
        await CloseAsync();
    }

    private async Task Cancel()
    {
        _resultTaskCompletionSource.TrySetResult(null);
        await CloseAsync();
    }
}
