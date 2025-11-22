using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Players;

public interface IPlayer
{
    Guid Id { get; }
    string Name { get; }
    IReadOnlyList<IUnit> Units { get; }
    IReadOnlyList<IUnit> AliveUnits { get; }
    
    bool CanAct { get; }
    
    PlayerStatus Status { get; set; }
    
    string Tint { get; }
    
    /// <summary>
    /// Indicates how this player is controlled 
    /// This is metadata, not behavior
    /// </summary>
    PlayerControlType ControlType { get; }
}