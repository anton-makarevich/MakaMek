using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Events;

/// <summary>
/// Represents a UI event that can be displayed in the UI
/// </summary>
public class UiEvent
{
    /// <summary>
    /// Creates a new UI event
    /// </summary>
    /// <param name="type">The type of event</param>
    /// <param name="parameters">Optional parameters for localization (e.g., damage amount)</param>
    public UiEvent(UiEventType type, params string[] parameters)
    {
        Type = type;
        Parameters = parameters;
    }

    /// <summary>
    /// The type of event
    /// </summary>
    public UiEventType Type { get; }
    
    /// <summary>
    /// Parameters for localization
    /// </summary>
    public string[] Parameters { get; }
}