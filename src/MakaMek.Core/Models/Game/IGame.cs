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
    PhaseStepState? PhaseStepState { get; }
    IObservable<int> TurnChanges { get; }
    IObservable<PhaseNames> PhaseChanges { get; }
    IObservable<PhaseStepState?> PhaseStepChanges { get; }

    IBattleMap? BattleMap { get; }
    void SetBattleMap(IBattleMap map);

    Guid Id { get; }

    IToHitCalculator ToHitCalculator { get; }
    IRulesProvider RulesProvider { get; }
}