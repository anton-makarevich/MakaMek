using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class BridgeCollapsedCommandTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly IUnit _unit = Substitute.For<IUnit>();

    public BridgeCollapsedCommandTests()
    {
        _unit.Id.Returns(_unitId);
        _unit.Model.Returns("LCT-1V");

        var player = Substitute.For<IPlayer>();
        player.Units.Returns([_unit]);

        _game.Players.Returns([player]);
    }

    private BridgeCollapsedCommand CreateCommand()
    {
        return new BridgeCollapsedCommand
        {
            GameOriginId = _gameId,
            Coordinates = new HexCoordinateData(1, 2),
            ConstructionFactor = 40,
            TotalTonnage = 20,
            TriggeringUnitId = _unitId,
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        var sut = CreateCommand();

        var result = sut.Render(_localizationService, _game);

        result.ShouldNotBeEmpty();
        result.ShouldContain("LCT-1V");
        result.ShouldContain("0102");
        result.ShouldContain("CF: 40");
        result.ShouldContain("Tonnage: 20");
    }

    [Fact]
    public void Render_ShouldUseUnitId_WhenUnitNotFound()
    {
        var unknownUnitId = Guid.NewGuid();
        var sut = CreateCommand() with { TriggeringUnitId = unknownUnitId };

        var result = sut.Render(_localizationService, _game);

        result.ShouldNotBeEmpty();
        result.ShouldContain(unknownUnitId.ToString());
    }

    [Fact]
    public void Render_ShouldIncludeCoordinates()
    {
        var sut = new BridgeCollapsedCommand
        {
            GameOriginId = _gameId,
            Coordinates = new HexCoordinateData(5, 3),
            ConstructionFactor = 30,
            TotalTonnage = 55,
            TriggeringUnitId = _unitId,
            Timestamp = DateTime.UtcNow
        };

        var result = sut.Render(_localizationService, _game);

        result.ShouldContain("0503");
    }

    [Fact]
    public void Render_ShouldIncludeConstructionFactorAndTonnage()
    {
        var sut = new BridgeCollapsedCommand
        {
            GameOriginId = _gameId,
            Coordinates = new HexCoordinateData(1, 2),
            ConstructionFactor = 50,
            TotalTonnage = 100,
            TriggeringUnitId = _unitId,
            Timestamp = DateTime.UtcNow
        };

        var result = sut.Render(_localizationService, _game);

        result.ShouldContain("CF: 50");
        result.ShouldContain("Tonnage: 100");
        result.ShouldContain("collapsed bridge at hex");
    }
}
