using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class HullBreachCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    public HullBreachCommandTests()
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

        _localizationService.GetString("Command_HullBreach_Header").Returns("{0} hull breached!");
        _localizationService.GetString("MechPart_CenterTorso_Short").Returns("CT");
        _localizationService.GetString("MechPart_LeftArm_Short").Returns("LA");
        _localizationService.GetString("Command_HullBreach_Automatic").Returns("{0} automatic hull breach");
        _localizationService.GetString("Command_HullBreach_Roll").Returns("{0} hull breach (roll {1})");
        _localizationService.GetString("Command_HullBreach_EngineDamage").Returns("Engine hit for {0} damage");
    }

    private HullBreachCommand CreateBasicCommand()
    {
        return new HullBreachCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            BreachedLocations =
            [
                new LocationHullBreachData(PartLocation.CenterTorso, false, [5, 6], null, 0)
            ],
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Render_ShouldFormatHeader_Correctly()
    {
        var sut = CreateBasicCommand();
        var result = sut.Render(_localizationService, _game);
        result.ShouldContain("LCT-1V hull breached!");
    }

    [Fact]
    public void Render_ShouldContainLocation()
    {
        var sut = CreateBasicCommand();
        var result = sut.Render(_localizationService, _game);
        result.ShouldContain("CT hull breach (roll 11)");
    }

    [Fact]
    public void Render_ShouldFormatAutomaticBreach()
    {
        var sut = new HullBreachCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            BreachedLocations =
            [
                new LocationHullBreachData(PartLocation.CenterTorso, true, null, null, 0)
            ],
            Timestamp = DateTime.UtcNow
        };

        var result = sut.Render(_localizationService, _game);
        result.ShouldContain("CT automatic hull breach");
    }

    [Fact]
    public void Render_ShouldIncludeEngineDamage_WhenPresent()
    {
        var sut = new HullBreachCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            BreachedLocations =
            [
                new LocationHullBreachData(PartLocation.CenterTorso, false, [5, 6], null, 3)
            ],
            Timestamp = DateTime.UtcNow
        };

        var result = sut.Render(_localizationService, _game);
        result.ShouldContain("Engine hit for 3 damage");
    }

    [Fact]
    public void Render_ShouldHandleMultipleLocations()
    {
        var sut = new HullBreachCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            BreachedLocations =
            [
                new LocationHullBreachData(PartLocation.CenterTorso, false, [5, 6], null, 0),
                new LocationHullBreachData(PartLocation.LeftArm, false, [4, 4], null, 0)
            ],
            Timestamp = DateTime.UtcNow
        };

        var result = sut.Render(_localizationService, _game);
        result.ShouldContain("CT");
        result.ShouldContain("LA");
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenUnitNotFound()
    {
        var sut = CreateBasicCommand() with { UnitId = Guid.NewGuid() };
        var result = sut.Render(_localizationService, _game);
        result.ShouldBeEmpty();
    }
}
