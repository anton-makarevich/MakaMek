using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;

public class MoveUnitCommandBuilder(Guid gameId, Guid playerId) : ClientCommandBuilder(gameId, playerId)
{
    private Guid? _unitId;
    private MovementPath? _movementPath;

    public override bool CanBuild => 
        _unitId != null 
        && _movementPath != null;

    public void SetUnit(IUnit unit)
    {
        _unitId = unit.Id;
    }

    public void SetMovementPath(MovementPath movementPath)
    {
        _movementPath = movementPath;
    }

    public MoveUnitCommand? Build()
    {
        if (_unitId == null || _movementPath == null)
            return null;

        return new MoveUnitCommand
        {
            GameOriginId = GameId,
            PlayerId = PlayerId,
            UnitId = _unitId.Value,
            MovementType = _movementPath.MovementType,
            MovementPath = _movementPath.ToData()
        };
    }

    public void Reset()
    {
        _unitId = null;
        _movementPath = null;
    }
}
