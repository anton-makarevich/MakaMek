using System;
using System.Threading.Tasks;
using Avalonia.Threading;
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
}
