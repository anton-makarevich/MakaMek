using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Commands.Client;

public class WeaponAttackDeclarationCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player1 = new Player(Guid.NewGuid(), "Player 1");
    private readonly Player _player2 = new Player(Guid.NewGuid(), "Player 2");
    private readonly Unit _attacker;
    private readonly Unit _target;

    public WeaponAttackDeclarationCommandTests()
    {
        _game.Players.Returns([_player1, _player2]);
        
        // Create an attacker unit
        var attackerData = MechFactoryTests.CreateDummyMechData();
        attackerData.Id=Guid.NewGuid();
        var mechFactory = new MechFactory(new ClassicBattletechRulesProvider(),_localizationService);
        _attacker = mechFactory.Create(attackerData);
        _player1.AddUnit(_attacker);
        
        // Create a target unit
        var targetData = MechFactoryTests.CreateDummyMechData();
        targetData.Id = Guid.NewGuid();
        _target = mechFactory.Create(targetData);
        _player2.AddUnit(_target);

        _localizationService.GetString("Command_WeaponAttackDeclaration_NoAttacks")
            .Returns("{0}'s {1} didn't declare any attacks");
        _localizationService.GetString("Command_WeaponAttackDeclaration_Header")
            .Returns("{0}'s {1} declared following attacks:");
        _localizationService.GetString("Command_WeaponAttackDeclaration_WeaponLine")
            .Returns("- {0} at {1}'s {2}");
    }

    private WeaponAttackDeclarationCommand CreateCommand()
    {
        return new WeaponAttackDeclarationCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            AttackerId = _attacker.Id,
            WeaponTargets =
            [
                new WeaponTargetData
                {
                    Weapon = new WeaponData
                    {
                        Name = "Medium Laser",
                        Location = PartLocation.RightArm,
                        Slots = [1, 2]
                    },
                    TargetId = _target.Id,
                    IsPrimaryTarget = true
                }
            ]
        };
    }
    
    [Fact]
    public void Render_ReturnsEmpty_WhenPlayerNotFound()
    {
        // Arrange
        var command = CreateCommand() with { PlayerId = Guid.NewGuid() };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ReturnsEmpty_WhenAttackerNotFound()
    {
        // Arrange
        var command = CreateCommand() with { AttackerId = Guid.NewGuid() };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ReturnsNoAttacksMessage_WhenNoWeaponTargets()
    {
        // Arrange
        var command = CreateCommand() with { WeaponTargets = new List<WeaponTargetData>() };
        _attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        _localizationService.Received(1).GetString("Command_WeaponAttackDeclaration_NoAttacks");
        result.ShouldBe($"{_player1.Name}'s {_attacker.Name} didn't declare any attacks");
    }

    [Fact]
    public void Render_SkipsInvalidTargets_WhenTargetNotFound()
    {
        // Arrange
        var command = CreateCommand();
        command.WeaponTargets.Add(new WeaponTargetData
        {
            Weapon = new WeaponData
            {
                Name = "Large Laser",
                Location = PartLocation.LeftArm,
                Slots = [1, 2, 3]
            },
            TargetId = Guid.NewGuid(), // Invalid target ID
            IsPrimaryTarget = false
        });
        
        _attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        _target.Deploy(new HexPosition(new HexCoordinates(2, 2), HexDirection.Top));

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        _localizationService.Received(1).GetString("Command_WeaponAttackDeclaration_Header");
        _localizationService.Received(1).GetString("Command_WeaponAttackDeclaration_WeaponLine");
        
        var expectedResult = $"{_player1.Name}'s {_attacker.Name} declared following attacks:" + Environment.NewLine +
                             $"- Medium Laser at {_player2.Name}'s {_target.Name}";
        
        result.ShouldBe(expectedResult);
    }
    
    [Fact]
    public void Render_ReturnsNoAttacksMessage_WhenTargetsNotFound()
    {
        // Arrange
        var command = CreateCommand();
        command.WeaponTargets.Clear();
        
        _attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        _target.Deploy(new HexPosition(new HexCoordinates(2, 2), HexDirection.Top));

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        _localizationService.Received(1).GetString("Command_WeaponAttackDeclaration_NoAttacks");

        var expectedResult = $"{_player1.Name}'s {_attacker.Name} didn't declare any attacks";
        
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public void Render_ReturnsAttackDeclarationMessage_WhenAllDataIsValid()
    {
        // Arrange
        var command = CreateCommand();
        _attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        _target.Deploy(new HexPosition(new HexCoordinates(2, 2), HexDirection.Top));

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        _localizationService.Received(1).GetString("Command_WeaponAttackDeclaration_Header");
        _localizationService.Received(1).GetString("Command_WeaponAttackDeclaration_WeaponLine");
        
        var expectedResult = $"{_player1.Name}'s {_attacker.Name} declared following attacks:" +Environment.NewLine +
                             $"- Medium Laser at {_player2.Name}'s {_target.Name}";
        
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public void Render_ReturnsMultipleWeaponLines_WhenMultipleWeaponsTarget()
    {
        // Arrange
        var command = CreateCommand();
        // Add a second weapon targeting the same unit
        command.WeaponTargets.Add(new WeaponTargetData
        {
            Weapon = new WeaponData
            {
                Name = "Large Laser",
                Location = PartLocation.LeftArm,
                Slots = [1, 2, 3]
            },
            TargetId = _target.Id,
            IsPrimaryTarget = false
        });
        
        _attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top));
        _target.Deploy(new HexPosition(new HexCoordinates(2, 2), HexDirection.Top));

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        _localizationService.Received(1).GetString("Command_WeaponAttackDeclaration_Header");
        _localizationService.Received(1).GetString("Command_WeaponAttackDeclaration_WeaponLine");
        
        var expectedResult = $"{_player1.Name}'s {_attacker.Name} declared following attacks:" + Environment.NewLine +
                             $"- Medium Laser at {_player2.Name}'s {_target.Name}" + Environment.NewLine +
                             $"- Large Laser at {_player2.Name}'s {_target.Name}";
        
        result.ShouldBe(expectedResult);
    }
}
