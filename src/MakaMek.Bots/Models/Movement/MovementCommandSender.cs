using Sanet.MakaMek.Bots.Exceptions;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Movement;

/// <summary>
/// Sends movement commands through the game client.
/// </summary>
public sealed class MovementCommandSender(IClientGame game, Random? random = null) : IMovementCommandSender
{
    private readonly Random _random = random ?? new Random();

    public async Task<bool> TryStandUp(IPlayer player, IUnit unit)
    {
        if (unit is not Mech { IsProne: true } mech || !mech.CanStandup())
            return false;

        // Select a random facing direction
        var newFacing = HexDirectionExtensions.AllDirections[
            _random.Next(HexDirectionExtensions.AllDirections.Length)];

        var command = new TryStandupCommand
        {
            GameOriginId = game.Id,
            PlayerId = player.Id,
            UnitId = mech.Id,
            NewFacing = newFacing,
            MovementTypeAfterStandup = MovementType.Walk
        };

        await game.TryStandupUnit(command);
        return true;
    }

    public async Task Move(IPlayer player, IUnit unit, MovementPath path)
    {
        var command = new MoveUnitCommand
        {
            GameOriginId = game.Id,
            PlayerId = player.Id,
            UnitId = unit.Id,
            MovementType = path.MovementType,
            MovementPath = path.ToData()
        };

        await game.MoveUnit(command);
    }

    public async Task SkipTurn(IPlayer player, IUnit? unit)
    {
        // Find any unit that hasn't moved yet (fallback)
        var unmovedUnit = unit ?? player.AliveUnits.FirstOrDefault(u => !u.HasMoved);
        var position = unmovedUnit?.Position;
        if (unmovedUnit == null || position == null)
        {
            throw new BotDecisionException(
                $"No unmoved units available for player {player.Name}",
                "MovementEngine",
                player.Id);
        }

        // Send a StandingStill movement command
        await Move(player, unmovedUnit, MovementPath.CreateSingleSegmentPath(position));
    }
}
