using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// Row/edit view model for a single relay hub entry in the Settings screen.
/// Built-in (Demo) hubs cannot be edited or removed.
/// </summary>
public class HubEntryViewModel : BindableBase
{
    private readonly Func<HubEntryViewModel, Task>? _onSaved;
    private readonly Action<HubEntryViewModel>? _onCancelled;
    private readonly Func<HubEntryViewModel, CancellationToken, Task<HubStatus>>? _checkStatus;
    private HubConfigData _hub;
    private string _editableName;
    private string _editableBaseUrl;
    private string _editableApiKey;
    private int _refreshGeneration;

    public HubEntryViewModel(
        HubConfigData hub,
        bool isNew = false,
        Func<HubEntryViewModel, Task>? onSaved = null,
        Action<HubEntryViewModel>? onCancelled = null,
        Func<HubEntryViewModel, CancellationToken, Task<HubStatus>>? checkStatus = null)
    {
        _hub = hub;
        IsNew = isNew;
        _onSaved = onSaved;
        _onCancelled = onCancelled;
        _checkStatus = checkStatus;
        _editableName = hub.Name;
        _editableBaseUrl = hub.BaseUrl;
        _editableApiKey = hub.ApiKey;

        StartEditingCommand = new AsyncCommand(StartEditing);
        SaveCommand = new AsyncCommand(Save);
        CancelCommand = new AsyncCommand(Cancel);
        RefreshStatusCommand = new AsyncCommand(() => RefreshStatusAsync());
    }

    public HubConfigData Hub => _hub;

    /// <summary>
    /// The hub configuration produced by the current edits, before it is committed.
    /// </summary>
    public HubConfigData PendingHub => _hub with
    {
        Name = string.IsNullOrWhiteSpace(EditableName) ? Name : EditableName.Trim(),
        BaseUrl = EditableBaseUrl.Trim(),
        ApiKey = EditableApiKey
    };

    /// <summary>
    /// Marks an entry that has not yet been persisted to the provider.
    /// </summary>
    public bool IsNew { get; }

    public string Id => _hub.Id;
    public string Name => _hub.Name;
    public string BaseUrl => _hub.BaseUrl;
    public string ApiKey => _hub.ApiKey;
    public bool IsBuiltIn => _hub.IsBuiltIn;

    public bool CanEdit => !IsBuiltIn;
    public bool CanRemove => !IsBuiltIn;

    public bool IsEditing
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Reachability state of this hub, surfaced by the status badge.
    /// </summary>
    public HubStatus Status
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// True while a health probe for this hub is in flight.
    /// </summary>
    public bool IsCheckingStatus
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string EditableName
    {
        get => _editableName;
        set => SetProperty(ref _editableName, value);
    }

    public string EditableBaseUrl
    {
        get => _editableBaseUrl;
        set => SetProperty(ref _editableBaseUrl, value);
    }

    public string EditableApiKey
    {
        get => _editableApiKey;
        set => SetProperty(ref _editableApiKey, value);
    }

    public ICommand StartEditingCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RefreshStatusCommand { get; }

    public Task StartEditing()
    {
        if (!CanEdit) return Task.CompletedTask;

        EditableName = Name;
        EditableBaseUrl = BaseUrl;
        EditableApiKey = _hub.ApiKey;
        IsEditing = true;
        return Task.CompletedTask;
    }

    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(EditableBaseUrl)) return;

        // Persist first; only commit the saved state and close the editor on success,
        // so a failed write keeps the edited values available for retry.
        if (_onSaved != null)
        {
            await _onSaved(this);
        }

        _hub = PendingHub;
        IsEditing = false;

        NotifyPropertyChanged(nameof(Hub));
        NotifyPropertyChanged(nameof(Name));
        NotifyPropertyChanged(nameof(BaseUrl));
    }

    private Task Cancel()
    {
        EditableName = Name;
        EditableBaseUrl = BaseUrl;
        EditableApiKey = _hub.ApiKey;
        IsEditing = false;
        _onCancelled?.Invoke(this);
        return Task.CompletedTask;
    }

    public async Task RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_checkStatus == null)
        {
            Status = HubStatus.Unknown;
            return;
        }

        var generation = Interlocked.Increment(ref _refreshGeneration);
        IsCheckingStatus = true;
        Status = HubStatus.Checking;
        try
        {
            var result = await _checkStatus(this, cancellationToken);
            if (generation != Volatile.Read(ref _refreshGeneration)) return;
            Status = result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (generation != Volatile.Read(ref _refreshGeneration)) return;
            Status = HubStatus.Unknown;
        }
        catch
        {
            if (generation != Volatile.Read(ref _refreshGeneration)) return;
            Status = HubStatus.Offline;
        }
        finally
        {
            if (generation == Volatile.Read(ref _refreshGeneration))
            {
                IsCheckingStatus = false;
            }
        }
    }
}
