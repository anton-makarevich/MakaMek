using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Models.Game;

namespace Sanet.MakaMek.Tools.BotContainer.Services;

public class GameStateProvider : IGameStateProvider
{
    public ClientGame? ClientGame { get; set; }
    public ITacticalEvaluator? TacticalEvaluator { get; set; }
}
