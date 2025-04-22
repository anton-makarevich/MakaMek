using NSubstitute;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public abstract class GamePhaseTestsBase
{
    protected ServerGame Game;
    protected readonly ICommandPublisher CommandPublisher;
    protected readonly IDiceRoller DiceRoller;
    protected readonly IPhaseManager MockPhaseManager;
    private readonly IMechFactory _mechFactory = new MechFactory(new ClassicBattletechRulesProvider(),Substitute.For<ILocalizationService>());

    protected GamePhaseTestsBase()
    {
        CommandPublisher = Substitute.For<ICommandPublisher>();
        DiceRoller = Substitute.For<IDiceRoller>();
        MockPhaseManager = Substitute.For<IPhaseManager>();
        IRulesProvider rulesProvider = new ClassicBattletechRulesProvider();
        
        Game = new ServerGame( rulesProvider, _mechFactory, CommandPublisher, DiceRoller,
            Substitute.For<IToHitCalculator>(), MockPhaseManager);
    }
    
    protected void SetGameWithRulesProvider(IRulesProvider rulesProvider)
    {
        Game = new ServerGame( rulesProvider, _mechFactory, CommandPublisher, DiceRoller,
            Substitute.For<IToHitCalculator>(), MockPhaseManager);
    }

    protected void SetMap()
    {
        var battleMap = BattleMapTests.BattleMapFactory.GenerateMap(10, 10, new SingleTerrainGenerator(10,10,
            new ClearTerrain()));
        Game.SetBattleMap(battleMap);
    }

    protected void VerifyPhaseChange(PhaseNames expectedPhaseNames)
    {
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<ChangePhaseCommand>(cmd => 
                cmd.Phase == expectedPhaseNames && 
                cmd.GameOriginId == Game.Id));
    }

    protected void VerifyActivePlayerChange(Guid? expectedPlayerId)
    {
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<ChangeActivePlayerCommand>(cmd => 
                cmd.PlayerId == expectedPlayerId && 
                cmd.GameOriginId == Game.Id));
    }

    protected JoinGameCommand CreateJoinCommand(Guid playerId, string playerName, int unitsCount=1)
    {
        List<UnitData> units = [];
        for (var i = 0; i < unitsCount ; i++)
        {
            var mechData = MechFactoryTests.CreateDummyMechData();
            mechData.Id = Guid.NewGuid();
            units.Add(mechData);
        }
        
        return new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PlayerName = playerName,
            Units = units,
            Tint = "#FF0000"
        };
    }

    protected UpdatePlayerStatusCommand CreateStatusCommand(Guid playerId, PlayerStatus status)
    {
        return new UpdatePlayerStatusCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PlayerStatus = status
        };
    }

    protected DeployUnitCommand CreateDeployCommand(Guid playerId, Guid unitId, int q, int r, int direction)
    {
        return new DeployUnitCommand
        {
            GameOriginId = Game.Id,
            PlayerId = playerId,
            UnitId = unitId,
            Position = new HexCoordinateData(q,r),
            Direction = direction
        };
    }
}
