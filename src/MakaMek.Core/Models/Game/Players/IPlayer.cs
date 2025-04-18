using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Players;

public interface IPlayer
{
    Guid Id { get; }
    string Name { get; }
    IReadOnlyList<Unit> Units { get; }
    IReadOnlyList<Unit> AliveUnits { get; }
    
    PlayerStatus Status { get; set; }
    
    string Tint { get; }
}