using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Players;

public class Player(Guid id, string name, string tint = "#ffffff") : IPlayer
{
    public Player(PlayerData data) : this(data.Id, data.Name, data.Tint)
    {
    }
    
    private readonly List<Unit> _units = [];
    
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public string Tint { get; } = tint;
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