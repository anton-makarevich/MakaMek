using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;

public class DeploymentCommandBuilder(Guid gameId, Guid playerId) : ClientCommandBuilder(gameId, playerId)
{
    private Guid? _unitId;
    private HexCoordinates? _position;
    private HexDirection? _direction;

    public override bool CanBuild => _unitId.HasValue && _position!=null && _direction.HasValue;
    
    public void SetUnit(Unit unit) => _unitId = unit.Id;
    public void SetPosition(HexCoordinates pos) => _position = pos;
    public void SetDirection(HexDirection dir) => _direction = dir;
    
    public void Reset()
    {
        _unitId = null;
        _position = null;
        _direction = null;
    }
    
    public DeployUnitCommand? Build()
    {
        if (_unitId == null || _position == null || _direction == null)
            return null;

        return new DeployUnitCommand
        {
            GameOriginId = GameId,
            PlayerId = PlayerId,
            UnitId = _unitId.Value,
            Position = _position.ToData(),
            Direction = (int)_direction.Value
        };
    }
}
