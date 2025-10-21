using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Server command sent when a client command is rejected (e.g., duplicate command detected)
/// </summary>
public record struct ErrorCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// The idempotency key of the rejected command
    /// </summary>
    public required Guid? IdempotencyKey { get; init; }
    
    /// <summary>
    /// Error code for programmatic handling
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }
    
    public string Render(ILocalizationService localizationService, IGame game)
    {
        var key = $"Command_Error_{ErrorCode}";
        return localizationService.GetString(key);
    }
}