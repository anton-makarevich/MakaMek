using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Players;

public class Player(Guid id, string name, PlayerControlType controlType, string tint = "#ffffff") : IPlayer
{
    public Player(PlayerData data, PlayerControlType controlType, Guid? idOverride = null) 
        : this(idOverride ?? data.Id, data.Name, controlType, data.Tint)
    {
    }
    
    private readonly List<Unit> _units = [];
    
    public Guid Id { get; } = id;
    public string Name { get; set; } = name;
    public string Tint { get; } = tint;
    public PlayerControlType ControlType { get; } = controlType;
    public IReadOnlyList<Unit> Units => _units;
    public IReadOnlyList<Unit> AliveUnits => Units.Where(u => u.Status != UnitStatus.Destroyed).ToList();
    public bool CanAct => AliveUnits.Count > 0 ;

    /// <summary>
    /// Returns only units that are not destroyed
    /// </summary>
    public PlayerStatus Status { get; set; } = PlayerStatus.NotJoined;

    public void AddUnit(Unit unit)
    {
        _units.Add(unit);
        unit.Owner = this;
    }
}