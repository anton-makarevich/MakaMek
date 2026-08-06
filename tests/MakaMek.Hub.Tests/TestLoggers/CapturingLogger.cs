using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Hub.Tests.TestLoggers;

/// <summary>
/// Test-only <see cref="ILogger{T}"/> that captures every formatted message by level so
/// tests can assert that specific log entries are produced.
/// </summary>
public sealed class CapturingLogger<T>(LogLevel minimumLevel = LogLevel.Trace) : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }

    public IEnumerable<string> GetMessages(LogLevel level) =>
        Entries.Where(entry => entry.Level == level).Select(entry => entry.Message);
}
