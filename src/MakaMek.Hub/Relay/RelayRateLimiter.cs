using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Sanet.MakaMek.Hub.Configuration;

namespace Sanet.MakaMek.Hub.Relay;

/// <summary>
/// Per-connection fixed-window rate limiter for <c>Relay()</c> calls.
/// </summary>
public interface IRelayRateLimiter
{
    /// <summary>
    /// Attempts to consume one permit for the given connection within the current window.
    /// </summary>
    /// <returns><see langword="true"/> when the call is allowed; otherwise <see langword="false"/>.</returns>
    bool TryConsume(string connectionId);

    /// <summary>
    /// Removes rate-limit state for a disconnected connection.
    /// </summary>
    void RemoveConnection(string connectionId);
}

/// <summary>
/// Thread-safe per-connection rate limiter backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Window boundaries are driven by the injected <see cref="TimeProvider"/>.
/// </summary>
public sealed class RelayRateLimiter : IRelayRateLimiter
{
    private readonly ConcurrentDictionary<string, ConnectionWindow> _windows = new(StringComparer.Ordinal);
    private readonly IOptions<HubOptions> _options;
    private readonly TimeProvider _timeProvider;

    public RelayRateLimiter(IOptions<HubOptions> options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }

    public bool TryConsume(string connectionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionId);

        var window = _windows.GetOrAdd(connectionId, static _ => new ConnectionWindow());
        var now = _timeProvider.GetUtcNow();
        var limit = _options.Value.RelayRateLimitPerMinute;

        lock (window)
        {
            if (now - window.WindowStart >= TimeSpan.FromMinutes(1))
            {
                window.WindowStart = now;
                window.Count = 0;
            }

            if (window.Count >= limit)
            {
                return false;
            }

            window.Count++;
            return true;
        }
    }

    public void RemoveConnection(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            return;
        }

        _windows.TryRemove(connectionId, out _);
    }

    private sealed class ConnectionWindow
    {
        public DateTimeOffset WindowStart { get; set; }
        public int Count { get; set; }
    }
}
