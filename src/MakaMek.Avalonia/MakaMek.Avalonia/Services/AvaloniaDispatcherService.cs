using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI.Avalonia;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>
/// Avalonia implementation of IDispatcherService using Dispatcher.UIThread.
/// </summary>
public class AvaloniaDispatcherService : IDispatcherService
{
    public void RunOnUIThread(Action action)
    {
        // Check if we are already on the UI thread
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            // Post the action to the UI thread's dispatcher queue
            Dispatcher.UIThread.Post(action);
        }
    }
    
    public async Task InvokeOnUIThread(Action action)
    {
        // Check if we are already on the UI thread
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            // Post the action to the UI thread's dispatcher queue
            await Dispatcher.UIThread.InvokeAsync(action);
        }
    }

    public void RunOnUIThread<TResult>(Func<TResult> callback)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            callback();
            return;
        }

        // Post the action to the UI thread's dispatcher queue
        Dispatcher.UIThread.InvokeAsync(callback);
    }

    public IScheduler Scheduler => AvaloniaScheduler.Instance;
}
