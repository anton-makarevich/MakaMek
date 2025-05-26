using Avalonia;

namespace Sanet.MakaMek.Avalonia.Services;

public class AvaloniaResourcesLocator : IAvaloniaResourcesLocator
{
    
    // Helper method to find resources in the application
    public object? TryFindResource(string key)
    {
        // Try to find in application resources
        if (Application.Current?.Resources.TryGetResource(key, null, out var resource)??false)
            return resource;
            
        return null;
    }
}