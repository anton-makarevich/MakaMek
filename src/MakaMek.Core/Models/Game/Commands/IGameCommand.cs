using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Commands;

public interface IGameCommand
{
    Guid GameOriginId { get; set; }
    DateTime Timestamp { get; set; } 
    string Render(ILocalizationService localizationService, IGame game); 
}