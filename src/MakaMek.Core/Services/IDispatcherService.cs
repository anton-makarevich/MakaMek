namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Provides a way to execute actions on the main UI thread.
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// Executes the specified action on the UI thread.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    void RunOnUIThread(Action action);

    Task InvokeOnUIThread(Action action);
}
