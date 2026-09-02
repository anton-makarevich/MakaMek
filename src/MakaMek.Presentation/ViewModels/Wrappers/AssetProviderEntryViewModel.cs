using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class AssetProviderEntryViewModel : BindableBase
{
    private readonly AssetProviderConfigData _provider;
    private readonly Action<AssetProviderEntryViewModel>? _onToggleActive;
    private readonly Action<AssetProviderEntryViewModel>? _onRemove;
    private bool _isActive;
    private bool _canDeactivate = true;

    public AssetProviderEntryViewModel(
        AssetProviderConfigData provider,
        Action<AssetProviderEntryViewModel>? onToggleActive = null,
        Action<AssetProviderEntryViewModel>? onRemove = null)
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

    private Task ExecuteToggleActive()
    {
        _onToggleActive?.Invoke(this);
        return Task.CompletedTask;
    }

    private Task ExecuteRemove()
    {
        _onRemove?.Invoke(this);
        return Task.CompletedTask;
    }
}
