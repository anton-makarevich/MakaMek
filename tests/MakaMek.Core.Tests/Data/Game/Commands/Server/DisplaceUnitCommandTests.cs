using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class DisplaceUnitCommandTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    public DisplaceUnitCommandTests()
    {
        var player = new Player(Guid.NewGuid(), "Player 1", PlayerControlType.Human);
        var mechFactory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        _unit = mechFactory.Create(unitData);
        player.AddUnit(_unit);
        _game.Players.Returns(new List<IPlayer> { player });
    }

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        var sut = new DisplaceUnitCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            FromCoordinates = new HexCoordinateData(1, 2),
            ToCoordinates = new HexCoordinateData(2, 2),
            NewFacing = (int)HexDirection.Top,
            DisplacementReason = DisplacementReason.DominoEffect,
            Timestamp = DateTime.UtcNow
        };

        var result = sut.Render(_localizationService, _game);

        result.ShouldNotBeEmpty();
        result.ShouldContain("LCT-1V");
        result.ShouldContain("0102");
        result.ShouldContain("0202");
    }

    [Fact]
    public void Render_ShouldUseUnitId_WhenUnitNotFound()
    {
        var unknownId = Guid.NewGuid();
        var sut = new DisplaceUnitCommand
        {
            GameOriginId = _gameId,
            UnitId = unknownId,
            FromCoordinates = new HexCoordinateData(1, 2),
            ToCoordinates = new HexCoordinateData(2, 2),
            NewFacing = (int)HexDirection.Top,
            DisplacementReason = DisplacementReason.DominoEffect,
            Timestamp = DateTime.UtcNow
        };

        var result = sut.Render(_localizationService, _game);

        result.ShouldContain(unknownId.ToString());
    }

    [Fact]
    public void Record_ShouldSupportEquality()
    {
        var cmd1 = new DisplaceUnitCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            FromCoordinates = new HexCoordinateData(1, 2),
            ToCoordinates = new HexCoordinateData(2, 2),
            NewFacing = (int)HexDirection.Top,
            DisplacementReason = DisplacementReason.DominoEffect,
            Timestamp = DateTime.UtcNow
        };

        var cmd2 = new DisplaceUnitCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            FromCoordinates = new HexCoordinateData(1, 2),
            ToCoordinates = new HexCoordinateData(2, 2),
            NewFacing = (int)HexDirection.Top,
            DisplacementReason = DisplacementReason.DominoEffect,
            Timestamp = DateTime.UtcNow
        };

        (cmd1 == cmd2).ShouldBeTrue();
        cmd1.GetHashCode().ShouldBe(cmd2.GetHashCode());
    }

    [Fact]
    public void Record_ShouldDetectInequality()
    {
        var cmd1 = new DisplaceUnitCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            FromCoordinates = new HexCoordinateData(1, 2),
            ToCoordinates = new HexCoordinateData(2, 2),
            NewFacing = (int)HexDirection.Top,
            DisplacementReason = DisplacementReason.DominoEffect,
            Timestamp = DateTime.UtcNow
        };

        var cmd2 = cmd1 with { ToCoordinates = new HexCoordinateData(3, 3) };

        (cmd1 != cmd2).ShouldBeTrue();
    }

    [Fact]
    public void ShouldHaveCorrectDefaults()
    {
        var sut = new DisplaceUnitCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            FromCoordinates = new HexCoordinateData(1, 2),
            ToCoordinates = new HexCoordinateData(2, 2),
            Timestamp = DateTime.UtcNow
        };

        sut.NewFacing.ShouldBe(0);
        sut.DisplacementReason.ShouldBe(DisplacementReason.DominoEffect);
    }

    [Fact]
    public void ShouldImplementIGameCommand()
    {
        var sut = new DisplaceUnitCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            FromCoordinates = new HexCoordinateData(1, 2),
            ToCoordinates = new HexCoordinateData(2, 2),
            Timestamp = DateTime.UtcNow
        };

        sut.ShouldBeAssignableTo<IGameCommand>();
    }
}
