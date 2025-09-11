using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public abstract class GamePhaseTestsBase
{
    protected ServerGame Game;
    protected readonly ICommandPublisher CommandPublisher = Substitute.For<ICommandPublisher>();
    protected readonly IDiceRoller DiceRoller= Substitute.For<IDiceRoller>();
    protected readonly IPhaseManager MockPhaseManager= Substitute.For<IPhaseManager>();
    protected readonly IFallProcessor MockFallProcessor= Substitute.For<IFallProcessor>();
    protected readonly IToHitCalculator MockToHitCalculator= Substitute.For<IToHitCalculator>();
    protected readonly IDamageTransferCalculator MockDamageTransferCalculator= Substitute.For<IDamageTransferCalculator>();
    protected readonly ICriticalHitsCalculator MockCriticalHitsCalculator= Substitute.For<ICriticalHitsCalculator>();
    protected readonly IConsciousnessCalculator MockConsciousnessCalculator= Substitute.For<IConsciousnessCalculator>();
    protected readonly IHeatEffectsCalculator MockHeatEffectsCalculator = Substitute.For<IHeatEffectsCalculator>();
    protected readonly IPilotingSkillCalculator MockPilotingSkillCalculator= Substitute.For<IPilotingSkillCalculator>();
    
    private readonly IMechFactory _mechFactory = new MechFactory(new ClassicBattletechRulesProvider(),Substitute.For<ILocalizationService>());

    protected GamePhaseTestsBase()
    {
        IRulesProvider rulesProvider = new ClassicBattletechRulesProvider();
        
        Game = new ServerGame( rulesProvider, _mechFactory, CommandPublisher, DiceRoller,
            MockToHitCalculator,
            MockDamageTransferCalculator,
            MockCriticalHitsCalculator,
            MockPilotingSkillCalculator,
            MockConsciousnessCalculator,
            MockHeatEffectsCalculator,
            MockFallProcessor,
            MockPhaseManager);
    }
    
    protected void SetGameWithRulesProvider(IRulesProvider rulesProvider)
    {
        Game = new ServerGame( rulesProvider, _mechFactory, CommandPublisher, DiceRoller,
            MockToHitCalculator,
            MockDamageTransferCalculator,
            MockCriticalHitsCalculator,
            MockPilotingSkillCalculator,
            MockConsciousnessCalculator,
            MockHeatEffectsCalculator,
            MockFallProcessor,
            MockPhaseManager);
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
        List<PilotAssignmentData> pilotAssignments = [];

        for (var i = 0; i < unitsCount ; i++)
        {
            var mechData = MechFactoryTests.CreateDummyMechData();
            mechData.Id = Guid.NewGuid();
            units.Add(mechData);

            // Create a default pilot for this unit
            var randomId = Guid.NewGuid().ToString()[..6];
            var pilotData = new PilotData
            {
                FirstName = "MechWarrior",
                LastName = randomId,
                Gunnery = 4,
                Piloting = 5,
                Health = 6,
                Injuries = 0,
                IsConscious = true
            };

            pilotAssignments.Add(new PilotAssignmentData
            {
                UnitId = mechData.Id!.Value,
                PilotData = pilotData
            });
        }

        return new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            PlayerName = playerName,
            Units = units,
            PilotAssignments = pilotAssignments,
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
