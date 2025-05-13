namespace Sanet.MakaMek.Core.Models.Units;

/// <summary>
/// Represents a UI event that can be displayed in the UI
/// </summary>
public class UiEvent
{
    /// <summary>
    /// Creates a new UI event
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="value">Optional value (e.g., damage amount)</param>
    public UiEvent(string message, string? value = null)
    {
        Message = message;
        Value = value;
    }

    /// <summary>
    /// The message to display
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// Optional value (e.g., damage amount)
    /// </summary>
    public string? Value { get; }
}