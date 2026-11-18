using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Receives change notifications from a setup coordinator so the owning screen can re-raise the same
/// property name on its own bindings.
/// </summary>
public interface ISetupNotificationSink
{
    void Notify(string? propertyName);
}

/// <summary>
/// Minimal observable state for the setup coordinators. Kept local to the setup coordinators instead
/// of deriving from the MVVM base so their coupling stays small.
/// </summary>
public abstract class SetupState : INotifyPropertyChanged
{
    private readonly ISetupNotificationSink _owner;

    protected SetupState(ISetupNotificationSink owner)
    {
        _owner = owner;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        _owner.Notify(propertyName);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        NotifyPropertyChanged(propertyName);
        return true;
    }
}
