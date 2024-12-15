using Sanet.MekForge.Core.Models.Units;

namespace Sanet.MekForge.Core.Game;

public class Player : IPlayer
{
    private readonly List<Unit> _units = [];
    
    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyList<Unit> Units => _units;

    public Player(Guid id, string name)
    {
        Id = id;
        Name = name;
    }
    
    public void AddUnit(Unit unit)
    {
        _units.Add(unit);
    }
}