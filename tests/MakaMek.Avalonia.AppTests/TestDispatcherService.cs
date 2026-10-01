using System.Reactive.Concurrency;
using global::Avalonia.Threading;
using Sanet.MakaMek.Services;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// A dispatcher service whose scheduler belongs to this instance rather than to the process.
///
/// The application's <c>AvaloniaDispatcherService</c> returns the static
/// <c>AvaloniaScheduler.Instance</c>, which binds to whichever dispatcher first uses it. Headless
/// tests isolate the application per test, so the second test to construct a view model subscribes
/// through a scheduler bound to a dispatcher that is already gone, and receives nothing at all.
/// That failure is silent: a view model with no commands looks exactly like a game that published
/// none.
///
/// The scheduler is built from the dispatcher's own synchronization context, captured when this is
/// constructed - which must therefore happen inside a dispatch. That keeps two properties the tests
/// depend on: delivery is genuinely deferred, which matters because deferred delivery is what caused
/// the defect this harness found and an immediate scheduler would hide it; and it still runs on the
/// UI thread, so a view model bound to a real view can raise collection changes without tripping
/// Avalonia's thread affinity checks.
/// </summary>
internal sealed class TestDispatcherService : IDispatcherService, IDisposable
{
    private readonly SynchronizationContextScheduler _scheduler =
        new(SynchronizationContext.Current
            ?? throw new InvalidOperationException(
                "Construct TestDispatcherService inside a dispatch: it captures the dispatcher's context."));

    public IScheduler Scheduler => _scheduler;

    public void RunOnUIThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    public async Task InvokeOnUIThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else await Dispatcher.UIThread.InvokeAsync(action);
    }

    public void RunOnUIThread<TResult>(Func<TResult> callback)
    {
        if (Dispatcher.UIThread.CheckAccess()) callback();
        else Dispatcher.UIThread.InvokeAsync(callback);
    }

    public void Dispose() { }
}
