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
    private readonly Func<AssetProviderEntryViewModel, Task>? _onSaved;
    private readonly Action<AssetProviderEntryViewModel>? _onCancelled;
    private bool _isActive;
    private bool _canDeactivate = true;
    private ProviderType _editableProviderType;
    private AssetType _editableAssetType;
    private string _editableUrlOrPath;

    public AssetProviderEntryViewModel(
        AssetProviderConfigData provider,
        Func<AssetProviderEntryViewModel, Task>? onToggleActive = null,
        Func<AssetProviderEntryViewModel, Task>? onRemove = null,
        Func<AssetProviderEntryViewModel, Task>? onSaved = null,
        Action<AssetProviderEntryViewModel>? onCancelled = null)
    {
        _provider = provider;
        _onToggleActive = onToggleActive;
        _onRemove = onRemove;
        _onSaved = onSaved;
        _onCancelled = onCancelled;
        _isActive = provider.IsActive;
        _editableProviderType = provider.ProviderType;
        _editableAssetType = provider.AssetType;
        _editableUrlOrPath = provider.UrlOrPath;

        ToggleActiveCommand = new AsyncCommand(ExecuteToggleActive);
        RemoveCommand = new AsyncCommand(ExecuteRemove);
        StartEditingCommand = new AsyncCommand(StartEditing);
        SaveCommand = new AsyncCommand(Save);
        CancelCommand = new AsyncCommand(Cancel);
    }

    public string Id => _provider.Id;
    public string UrlOrPath => _provider.UrlOrPath;
    public ProviderType ProviderType => _provider.ProviderType;
    public AssetType AssetType => _provider.AssetType;
    public bool IsDefault => _provider.IsDefault;
    public int SortOrder => _provider.SortOrder;

    /// <summary>
    /// The provider configuration produced by the current edits, before it is committed.
    /// </summary>
    public AssetProviderConfigData PendingProvider => _provider with
    {
        ProviderType = EditableProviderType,
        AssetType = EditableAssetType,
        UrlOrPath = EditableUrlOrPath.Trim()
    };

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
    public bool CanEdit => !IsDefault;

    public bool IsEditing
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public ProviderType EditableProviderType
    {
        get => _editableProviderType;
        set => SetProperty(ref _editableProviderType, value);
    }

    public AssetType EditableAssetType
    {
        get => _editableAssetType;
        set => SetProperty(ref _editableAssetType, value);
    }

    public string EditableUrlOrPath
    {
        get => _editableUrlOrPath;
        set => SetProperty(ref _editableUrlOrPath, value);
    }

    public ICommand ToggleActiveCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand StartEditingCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public Task StartEditing()
    {
        if (!CanEdit) return Task.CompletedTask;

        EditableProviderType = ProviderType;
        EditableAssetType = AssetType;
        EditableUrlOrPath = UrlOrPath;
        IsEditing = true;
        return Task.CompletedTask;
    }

    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(EditableUrlOrPath)) return;

        // Persist first; only commit the saved state and close the editor on success,
        // so a failed write keeps the edited values available for retry.
        if (_onSaved != null)
        {
            await _onSaved(this);
        }

        IsEditing = false;
    }

    private Task Cancel()
    {
        EditableProviderType = ProviderType;
        EditableAssetType = AssetType;
        EditableUrlOrPath = UrlOrPath;
        IsEditing = false;
        _onCancelled?.Invoke(this);
        return Task.CompletedTask;
    }

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
