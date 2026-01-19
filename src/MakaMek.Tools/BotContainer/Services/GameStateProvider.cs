using Sanet.MakaMek.Core.Models.Game;

namespace MakaMek.Tools.BotContainer.Services;

public class GameStateProvider : IGameStateProvider
{
    public ClientGame? ClientGame { get; set; }
}
