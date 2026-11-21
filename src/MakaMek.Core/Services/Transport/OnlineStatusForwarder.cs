using System.Reactive.Subjects;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Default <see cref="IOnlineStatusForwarder"/> implementation. Holds a
/// <see cref="BehaviorSubject{ConnectionStatus}"/> and an optional subscription to the
/// transport adapter's connection status stream.
/// </summary>
public class OnlineStatusForwarder : IOnlineStatusForwarder, IDisposable
{
    private readonly BehaviorSubject<ConnectionStatus> _onlineStatus = new(ConnectionStatus.NotConnected);
    private IDisposable? _subscription;
    private bool _isDisposed;

    /// <inheritdoc />
    public IObservable<ConnectionStatus> OnlineConnectionStatus => _onlineStatus;

    /// <inheritdoc />
    public void Start(ICommandTransportAdapter adapter)
    {
        _subscription?.Dispose();
        _subscription = adapter.ConnectionStatusChanges.Subscribe(_onlineStatus);
    }

    /// <inheritdoc />
    public void Reset()
    {
        _subscription?.Dispose();
        _subscription = null;
        _onlineStatus.OnNext(ConnectionStatus.NotConnected);
    }

    /// <summary>
    /// Releases the owned subscription and status subject. This forwarder is a DI singleton shared
    /// by the host and client flows, so the container disposes it at shutdown; the adapter it
    /// subscribes to is injected and is not owned here. Idempotent: repeated calls are no-ops
    /// because completing an already disposed <see cref="BehaviorSubject{T}"/> throws.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _subscription?.Dispose();
        _subscription = null;
        _onlineStatus.OnCompleted();
        _onlineStatus.Dispose();
        GC.SuppressFinalize(this);
    }
}
