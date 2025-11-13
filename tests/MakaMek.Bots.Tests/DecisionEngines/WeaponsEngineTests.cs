using NSubstitute;
using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Tests.Utils;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.DecisionEngines;

public class WeaponsEngineTests
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly WeaponsEngine _sut;
    private readonly BattleMap _battleMap;

    public WeaponsEngineTests()
    {
        _clientGame = Substitute.For<ClientGame>();
        _player = Substitute.For<IPlayer>();
        _battleMap = BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10, 10, new ClearTerrain()));
        
        _clientGame.BattleMap.Returns(_battleMap);
        _clientGame.Id.Returns(Guid.NewGuid());
        _player.Id.Returns(Guid.NewGuid());
        
        _sut = new WeaponsEngine(_clientGame, _player, BotDifficulty.Easy);
    }

    [Fact]
    public async Task MakeDecision_ShouldDeclareAttack_WhenUnitHasNotAttacked()
    {
        // Arrange
        var attacker = Substitute.For<Unit>();
        attacker.Id.Returns(Guid.NewGuid());
        attacker.HasDeclaredWeaponAttack.Returns(false);
        attacker.CanFireWeapons.Returns(true);
        attacker.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        attacker.GetAvailableComponents<Weapon>().Returns(new List<Weapon>());
        
        var aliveUnits = new List<Unit> { attacker };
        _player.AliveUnits.Returns(aliveUnits);
        _player.Id.Returns(Guid.NewGuid());
        _clientGame.Players.Returns(new List<IPlayer> { _player });
        _clientGame.DeclareWeaponAttack(Arg.Any<WeaponAttackDeclarationCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).DeclareWeaponAttack(Arg.Is<WeaponAttackDeclarationCommand>(cmd =>
            cmd.UnitId == attacker.Id &&
            cmd.PlayerId == _player.Id &&
            cmd.GameOriginId == _clientGame.Id));
    }

    [Fact]
    public async Task MakeDecision_ShouldNotDeclareAttack_WhenAllUnitsAttacked()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.HasDeclaredWeaponAttack.Returns(true);
        
        var aliveUnits = new List<Unit> { unit };
        _player.AliveUnits.Returns(aliveUnits);

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.DidNotReceive().DeclareWeaponAttack(Arg.Any<WeaponAttackDeclarationCommand>());
    }

    [Fact]
    public async Task MakeDecision_ShouldNotDeclareAttack_WhenUnitCannotFireWeapons()
    {
        // Arrange
        var unit = Substitute.For<Unit>();
        unit.HasDeclaredWeaponAttack.Returns(false);
        unit.CanFireWeapons.Returns(false);
        
        var aliveUnits = new List<Unit> { unit };
        _player.AliveUnits.Returns(aliveUnits);

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.DidNotReceive().DeclareWeaponAttack(Arg.Any<WeaponAttackDeclarationCommand>());
    }

    [Fact]
    public async Task MakeDecision_ShouldDeclareEmptyAttack_WhenNoTargetsAvailable()
    {
        // Arrange
        var attacker = Substitute.For<Unit>();
        attacker.Id.Returns(Guid.NewGuid());
        attacker.HasDeclaredWeaponAttack.Returns(false);
        attacker.CanFireWeapons.Returns(true);
        attacker.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        attacker.GetAvailableComponents<Weapon>().Returns(new List<Weapon>());
        
        var aliveUnits = new List<Unit> { attacker };
        _player.AliveUnits.Returns(aliveUnits);
        _player.Id.Returns(Guid.NewGuid());
        _clientGame.Players.Returns(new List<IPlayer> { _player }); // No enemy players
        _clientGame.DeclareWeaponAttack(Arg.Any<WeaponAttackDeclarationCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).DeclareWeaponAttack(Arg.Is<WeaponAttackDeclarationCommand>(cmd =>
            cmd.WeaponTargets.Count == 0));
    }

    [Fact]
    public async Task MakeDecision_ShouldSelectWeaponsInRange_WhenTargetAvailable()
    {
        // Arrange
        var weapon = Substitute.For<Weapon>();
        weapon.IsInRange(Arg.Any<int>()).Returns(true);
        
        var attacker = Substitute.For<Unit>();
        attacker.Id.Returns(Guid.NewGuid());
        attacker.HasDeclaredWeaponAttack.Returns(false);
        attacker.CanFireWeapons.Returns(true);
        attacker.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        attacker.GetAvailableComponents<Weapon>().Returns(new List<Weapon> { weapon });
        
        var target = Substitute.For<Unit>();
        target.Id.Returns(Guid.NewGuid());
        target.IsDeployed.Returns(true);
        target.Position.Returns(new HexPosition(new HexCoordinates(7, 7), HexDirection.Top));
        
        var enemyPlayer = Substitute.For<IPlayer>();
        enemyPlayer.Id.Returns(Guid.NewGuid());
        enemyPlayer.AliveUnits.Returns(new List<Unit> { target });
        
        _player.AliveUnits.Returns(new List<Unit> { attacker });
        _clientGame.Players.Returns(new List<IPlayer> { _player, enemyPlayer });
        _clientGame.DeclareWeaponAttack(Arg.Any<WeaponAttackDeclarationCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).DeclareWeaponAttack(Arg.Is<WeaponAttackDeclarationCommand>(cmd =>
            cmd.WeaponTargets.Count > 0 &&
            cmd.WeaponTargets.All(wt => wt.TargetId == target.Id)));
    }

    [Fact]
    public async Task MakeDecision_ShouldDeclareEmptyAttack_WhenNoWeaponsInRange()
    {
        // Arrange
        var weapon = Substitute.For<Weapon>();
        weapon.IsInRange(Arg.Any<int>()).Returns(false); // Weapon not in range
        
        var attacker = Substitute.For<Unit>();
        attacker.Id.Returns(Guid.NewGuid());
        attacker.HasDeclaredWeaponAttack.Returns(false);
        attacker.CanFireWeapons.Returns(true);
        attacker.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        attacker.GetAvailableComponents<Weapon>().Returns(new List<Weapon> { weapon });
        
        var target = Substitute.For<Unit>();
        target.Id.Returns(Guid.NewGuid());
        target.IsDeployed.Returns(true);
        target.Position.Returns(new HexPosition(new HexCoordinates(7, 7), HexDirection.Top));
        
        var enemyPlayer = Substitute.For<IPlayer>();
        enemyPlayer.Id.Returns(Guid.NewGuid());
        enemyPlayer.AliveUnits.Returns(new List<Unit> { target });
        
        _player.AliveUnits.Returns(new List<Unit> { attacker });
        _clientGame.Players.Returns(new List<IPlayer> { _player, enemyPlayer });
        _clientGame.DeclareWeaponAttack(Arg.Any<WeaponAttackDeclarationCommand>()).Returns(Task.FromResult(true));

        // Act
        await _sut.MakeDecision();

        // Assert
        await _clientGame.Received(1).DeclareWeaponAttack(Arg.Is<WeaponAttackDeclarationCommand>(cmd =>
            cmd.WeaponTargets.Count == 0));
    }

    [Fact]
    public async Task MakeDecision_ShouldHandleException_WhenAttackFails()
    {
        // Arrange
        var attacker = Substitute.For<Unit>();
        attacker.Id.Returns(Guid.NewGuid());
        attacker.HasDeclaredWeaponAttack.Returns(false);
        attacker.CanFireWeapons.Returns(true);
        attacker.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        attacker.GetAvailableComponents<Weapon>().Returns(new List<Weapon>());
        
        _player.AliveUnits.Returns(new List<Unit> { attacker });
        _clientGame.Players.Returns(new List<IPlayer> { _player });
        _clientGame.DeclareWeaponAttack(Arg.Any<WeaponAttackDeclarationCommand>())
            .Returns(Task.FromException<bool>(new Exception("Test exception")));

        // Act & Assert - should not throw
        await _sut.MakeDecision();
    }

    [Fact]
    public async Task MakeDecision_ShouldNotThrow_WhenBattleMapIsNull()
    {
        // Arrange
        _clientGame.BattleMap.Returns((BattleMap?)null);
        
        var attacker = Substitute.For<Unit>();
        attacker.HasDeclaredWeaponAttack.Returns(false);
        attacker.CanFireWeapons.Returns(true);
        attacker.Position.Returns(new HexPosition(new HexCoordinates(5, 5), HexDirection.Top));
        
        _player.AliveUnits.Returns(new List<Unit> { attacker });

        // Act & Assert - should not throw
        await _sut.MakeDecision();
    }

    [Fact]
    public async Task MakeDecision_ShouldNotThrow_WhenAttackerPositionIsNull()
    {
        // Arrange
        var attacker = Substitute.For<Unit>();
        attacker.HasDeclaredWeaponAttack.Returns(false);
        attacker.CanFireWeapons.Returns(true);
        attacker.Position.Returns((HexPosition?)null);
        
        _player.AliveUnits.Returns(new List<Unit> { attacker });

        // Act & Assert - should not throw
        await _sut.MakeDecision();
    }
}

