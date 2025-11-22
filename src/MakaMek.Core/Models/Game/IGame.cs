using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Models.Game;

public interface IGame
{
    public IReadOnlyList<IPlayer> Players { get; }
    public int Turn { get; }
    public PhaseNames TurnPhase { get; }
    public IPlayer? ActivePlayer { get; }
    public int UnitsToPlayCurrentStep { get; }

    public IObservable<int> TurnChanges { get; }
    public IObservable<PhaseNames> PhaseChanges { get; }
    public IObservable<IPlayer?> ActivePlayerChanges { get; }
    public IObservable<int> UnitsToPlayChanges { get; }

    public IBattleMap? BattleMap { get; }
    public void SetBattleMap(IBattleMap map);

    public Guid Id { get; }

    public IToHitCalculator ToHitCalculator { get; }
    public IRulesProvider RulesProvider { get; }
}