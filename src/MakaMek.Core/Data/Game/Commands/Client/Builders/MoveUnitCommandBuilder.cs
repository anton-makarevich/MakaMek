using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;

public class MoveUnitCommandBuilder(Guid gameId, Guid playerId) : ClientCommandBuilder(gameId, playerId)
{
    private Guid? _unitId;
    private MovementPath? _movementPath;
    private Guid? _movementAttackTargetId;

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

    /// <summary>
    /// Declares a Charge or DFA against the target as part of this move. The attack type follows
    /// the movement type once the path is set: a jump is a DFA, a walk or run is a charge.
    /// </summary>
    public void SetMovementAttackTarget(IUnit target)
    {
        _movementAttackTargetId = target.Id;
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
            MovementPath = _movementPath.ToData(),
            IsCompleted = true,
            Declaration = CreateDeclaration()
        };
    }

    public void Reset()
    {
        _unitId = null;
        _movementPath = null;
        _movementAttackTargetId = null;
    }

    private PhysicalAttackDeclarationData? CreateDeclaration()
    {
        if (_unitId == null || _movementPath == null || _movementAttackTargetId is not { } targetId)
            return null;

        var attackType = _movementPath.MovementType switch
        {
            MovementType.Jump => PhysicalAttackType.DFA,
            MovementType.Walk or MovementType.Run => PhysicalAttackType.Charge,
            _ => (PhysicalAttackType?)null
        };
        if (attackType is not { } type) return null;

        return new PhysicalAttackDeclarationData
        {
            AttackType = type,
            AttackerLimbs = [],
            TargetId = targetId
        };
    }
}
