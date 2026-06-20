using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class MechSkidCommandTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    public MechSkidCommandTests()
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

    private static FallingDamageData CreateTestFallingDamageData(int totalDamage = 5)
    {
        var hitLocations = new List<LocationHitData>
        {
            new LocationHitData(
                new List<LocationDamageData>
                {
                    new LocationDamageData(PartLocation.CenterTorso, totalDamage, 0, false)
                },
                [],
                [6],
                PartLocation.CenterTorso)
        };
        var hitLocationsData = new HitLocationsData(hitLocations, totalDamage);
        return new FallingDamageData(HexDirection.Top, hitLocationsData, new DiceResult(1), HitDirection.Front);
    }

    private MechSkidCommand CreateBasicSkidCommand()
    {
        return new MechSkidCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            SkidDistance = 2,
            DamageData = CreateTestFallingDamageData(),
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void ShouldImplementIGameCommand()
    {
        var sut = CreateBasicSkidCommand();
        sut.ShouldBeAssignableTo<IGameCommand>();
    }

    [Fact]
    public void Render_ShouldFormatSkidCommand_Correctly()
    {
        var sut = CreateBasicSkidCommand();

        var result = sut.Render(_localizationService, _game);

        result.ShouldNotBeEmpty();
        result.ShouldContain("LCT-1V skidded 2 hexes (skid damage)");
        result.ShouldContain("and took 5 damage");
    }

    [Fact]
    public void Render_ShouldIncludeHitLocations_WhenDamageDataHasHitLocations()
    {
        var sut = CreateBasicSkidCommand();

        var result = sut.Render(_localizationService, _game);

        result.ShouldNotBeEmpty();
        result.ShouldContain("Hit Locations:");
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenUnitNotFound()
    {
        var sut = CreateBasicSkidCommand() with { UnitId = Guid.NewGuid() };

        var result = sut.Render(_localizationService, _game);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenDamageDataIsNull()
    {
        var sut = CreateBasicSkidCommand() with { DamageData = null };

        var result = sut.Render(_localizationService, _game);

        result.ShouldBeEmpty();
    }
}
