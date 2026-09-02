using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class AssetProviderEntryViewModel : BindableBase
{
    private readonly AssetProviderConfigData _provider;
    private readonly Func<AssetProviderEntryViewModel, Task>? _onToggleActive;
    private readonly Func<AssetProviderEntryViewModel, Task>? _onRemove;
    private bool _isActive;
    private bool _canDeactivate = true;

    public AssetProviderEntryViewModel(
        AssetProviderConfigData provider,
        Func<AssetProviderEntryViewModel, Task>? onToggleActive = null,
        Func<AssetProviderEntryViewModel, Task>? onRemove = null)
    {
        _provider = provider;
        _onToggleActive = onToggleActive;
        _onRemove = onRemove;
        _isActive = provider.IsActive;

        ToggleActiveCommand = new AsyncCommand(ExecuteToggleActive);
        RemoveCommand = new AsyncCommand(ExecuteRemove);
    }

    public string Id => _provider.Id;
    public string UrlOrPath => _provider.UrlOrPath;
    public ProviderType ProviderType => _provider.ProviderType;
    public AssetType AssetType => _provider.AssetType;
    public bool IsDefault => _provider.IsDefault;
    public int SortOrder => _provider.SortOrder;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            NotifyPropertyChanged();
        }
    }

    public bool CanDeactivate
    {
        get => _canDeactivate;
        set
        {
            if (_canDeactivate == value) return;
            _canDeactivate = value;
            NotifyPropertyChanged();
        }
    }

    public bool CanRemove => !IsDefault;

    public ICommand ToggleActiveCommand { get; }
    public ICommand RemoveCommand { get; }

    private async Task ExecuteToggleActive()
    {
        if (_onToggleActive != null)
            await _onToggleActive(this);
    }

    private async Task ExecuteRemove()
    {
        if (_onRemove != null)
            await _onRemove(this);
    }
}
