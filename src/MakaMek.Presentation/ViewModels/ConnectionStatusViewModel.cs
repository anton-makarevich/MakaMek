using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// Child ViewModel that encapsulates connection-status subscription and derived state.
/// Composed into parent VMs that need to observe a <see cref="IObservable{ConnectionStatus}"/>
/// source and expose <see cref="OnlineConnectionStatus"/> / <see cref="IsConnectionDegraded"/>.
/// </summary>
public class ConnectionStatusViewModel : BaseViewModel, IDisposable
{
    private IDisposable? _subscription;

    public ConnectionStatusViewModel(IObservable<ConnectionStatus>? source, IScheduler scheduler)
    {
        Subscribe(source, scheduler);
    }

    /// <summary>
    /// Starts (or restarts) observing the given status source, disposing any previous subscription.
    /// A null source only clears the previous subscription.
    /// </summary>
    public void Subscribe(IObservable<ConnectionStatus>? source, IScheduler scheduler)
    {
        _subscription?.Dispose();
        _subscription = null;
        if (source == null) return;

        _subscription = source
            .ObserveOn(scheduler)
            .Subscribe(status => OnlineConnectionStatus = status);
    }

    /// <summary>
    /// The latest connection status reported by the transport.
    /// </summary>
    public ConnectionStatus OnlineConnectionStatus
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(IsConnectionDegraded));
        }
    }

    /// <summary>
    /// Whether the transport is in a degraded state (reconnecting, disconnected, or closed).
    /// </summary>
    public bool IsConnectionDegraded => OnlineConnectionStatus is ConnectionStatus.Reconnecting
        or ConnectionStatus.Disconnected
        or ConnectionStatus.Closed;

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
        GC.SuppressFinalize(this);
    }
}