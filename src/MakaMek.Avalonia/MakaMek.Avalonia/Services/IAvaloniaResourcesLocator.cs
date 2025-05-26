namespace Sanet.MakaMek.Avalonia.Services;

public interface IAvaloniaResourcesLocator
{
    object? TryFindResource(string key);
}