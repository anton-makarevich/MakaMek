namespace Sanet.MakaMek.Avalonia.Utils;

public interface IAvaloniaResourcesLocator
{
    object? TryFindResource(string key);
}