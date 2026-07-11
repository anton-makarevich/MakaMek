namespace Sanet.MakaMek.Avalonia.Controls.Services;

public interface IAvaloniaResourcesLocator
{
    object? TryFindResource(string key);
}
