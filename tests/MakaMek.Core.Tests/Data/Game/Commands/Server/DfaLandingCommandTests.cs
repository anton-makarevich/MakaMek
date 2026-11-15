using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.TestHelpers;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class DfaLandingCommandTests
{
    private readonly ILocalizationService _localizationService = TestLocalizationService.Create();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    public DfaLandingCommandTests()
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
    public void Render_ShouldNameTheUnitAndTheLandingKind()
    {
        var sut = new DfaLandingCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            LandingKind = DfaLandingKind.AttackerLanding,
            Timestamp = DateTime.UtcNow
        };

        var result = sut.Render(_localizationService, _game);

        result.ShouldContain("LCT-1V");
        result.ShouldContain("Death From Above");
    }

    [Fact]
    public void Render_WhenTheUnitIsNotInTheGame_ShowsItsId()
    {
        var unitId = Guid.NewGuid();
        var sut = new DfaLandingCommand
        {
            GameOriginId = _gameId,
            UnitId = unitId,
            LandingKind = DfaLandingKind.MissLanding,
            Timestamp = DateTime.UtcNow
        };

        var result = sut.Render(_localizationService, _game);

        result.ShouldContain(unitId.ToString());
        result.ShouldContain("miss");
    }
}
