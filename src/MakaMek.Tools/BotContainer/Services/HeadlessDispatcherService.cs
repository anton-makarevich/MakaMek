using System.Reactive.Concurrency;
using Sanet.MakaMek.Core.Services;

namespace MakaMek.Tools.BotContainer.Services;

public class HeadlessDispatcherService : IDispatcherService
{
    public IScheduler Scheduler => System.Reactive.Concurrency.Scheduler.Immediate;

    public void RunOnUIThread(Action action)
    {
        action();
    }

    public Task InvokeOnUIThread(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    public void RunOnUIThread<TResult>(Func<TResult> callback)
    {
        callback();
    }
}
