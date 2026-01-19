using Sanet.MakaMek.Core.Models.Game;

namespace MakaMek.Tools.BotContainer.Services;

public interface IGameStateProvider
{
    ClientGame? ClientGame { get; set; }
}
