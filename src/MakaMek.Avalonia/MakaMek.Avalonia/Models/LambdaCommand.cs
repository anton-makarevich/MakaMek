using System;
using System.Windows.Input;

namespace Sanet.MakaMek.Avalonia.Models;

public class LambdaCommand(Action execute) : ICommand
{
    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();

    public event EventHandler? CanExecuteChanged;
}
