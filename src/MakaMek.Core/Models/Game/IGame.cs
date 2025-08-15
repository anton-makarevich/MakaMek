using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Models.Game;

public interface IGame
{
    IReadOnlyList<IPlayer> Players { get; }
    int Turn { get; }
    PhaseNames TurnPhase { get; }
    IPlayer? ActivePlayer { get; }
    int UnitsToPlayCurrentStep { get; }

    IObservable<int> TurnChanges { get; }
    IObservable<PhaseNames> PhaseChanges { get; }
    IObservable<IPlayer?> ActivePlayerChanges { get; }
    IObservable<int> UnitsToPlayChanges { get; }
    
    BattleMap? BattleMap { get; }
    void SetBattleMap(BattleMap map);
    
    Guid Id { get; }
    
    IToHitCalculator ToHitCalculator { get; }
    IRulesProvider RulesProvider { get; }
}