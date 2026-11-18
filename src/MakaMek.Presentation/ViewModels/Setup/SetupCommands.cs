using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Creates the lobby commands so the setup base does not have to depend on the command
/// implementation.
/// </summary>
public static class SetupCommands
{
    public static ICommand CreatePlayerCommand(Func<Task> execute) => new AsyncCommand(execute);

    public static ICommand CreateRemovePlayerCommand(Func<PlayerViewModel?, Task> execute) =>
        new AsyncCommand<PlayerViewModel?>(execute);
}
