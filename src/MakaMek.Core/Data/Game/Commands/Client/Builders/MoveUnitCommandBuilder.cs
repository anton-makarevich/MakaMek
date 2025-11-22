using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;

public class MoveUnitCommandBuilder(Guid gameId, Guid playerId) : ClientCommandBuilder(gameId, playerId)
{
    private Guid? _unitId;
    private MovementType? _movementType;
    private List<PathSegment>? _movementPath;

    public override bool CanBuild => 
        _unitId != null 
        && _movementType != null 
        && _movementPath != null;

    public void SetUnit(IUnit unit)
    {
        _unitId = unit.Id;
    }

    public void SetMovementType(MovementType movementType)
    {
        _movementType = movementType;
    }

    public void SetMovementPath(List<PathSegment> movementPath)
    {
        _movementPath = movementPath;
    }

    public MoveUnitCommand? Build()
    {
        if (_unitId == null || _movementType == null || _movementPath == null)
            return null;

        return new MoveUnitCommand
        {
            GameOriginId = GameId,
            PlayerId = PlayerId,
            UnitId = _unitId.Value,
            MovementType = _movementType.Value,
            MovementPath = _movementPath.Select(s => s.ToData()).ToList()
        };
    }

    public void Reset()
    {
        _unitId = null;
        _movementType = null;
        _movementPath = null;
    }
}
