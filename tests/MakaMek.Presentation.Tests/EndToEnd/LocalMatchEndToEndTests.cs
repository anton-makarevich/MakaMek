using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.Transport;
using Sanet.Transport.Rx;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.EndToEnd;

/// <summary>
/// Exercises a local match through the same serialized command transport used by
/// the desktop host. This is intentionally kept at the presentation test boundary
/// so future UI workflows can be added without coupling them to Avalonia controls.
/// </summary>
public sealed class LocalMatchEndToEndTests : IDisposable
{
    private readonly RxTransportPublisher _transport = new();
    private readonly CommandTransportAdapter _adapter;
    private readonly CommandPublisher _publisher;
    private readonly ServerGame _server;
    private readonly ClientGame _clientOne;
    private readonly ClientGame _clientTwo;
    private readonly IPlayer _playerOne;
    private readonly IPlayer _playerTwo;
    private readonly UnitData _unitData;

    public LocalMatchEndToEndTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        _adapter = new CommandTransportAdapter(loggerFactory, _transport);
        _publisher = new CommandPublisher(_adapter, loggerFactory);

        var rules = new TotalWarfareRulesProvider();
        var localization = Substitute.For<ILocalizationService>();
        var mechFactory = new MechFactory(rules, new ClassicBattletechComponentProvider(), localization);
        _unitData = MechFactoryTests.CreateDummyMechData();

        var diceRoller = Substitute.For<IDiceRoller>();
        diceRoller.Roll2D6().Returns([new DiceResult(6), new DiceResult(6)],
            [new DiceResult(5), new DiceResult(5)],
            [new DiceResult(6), new DiceResult(6)]);
        _server = new ServerGame(rules, mechFactory, _publisher,
            diceRoller, Substitute.For<IToHitCalculator>(),
            new DamageTransferCalculator(mechFactory), Substitute.For<ICriticalHitsCalculator>(),
            Substitute.For<IHullBreachCalculator>(), Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(), Substitute.For<IHeatEffectsCalculator>(),
            Substitute.For<IFallProcessor>(), Substitute.For<IWeaponAttackResolver>(),
            loggerFactory.CreateLogger<ServerGame>());

        _clientOne = CreateClient(rules, mechFactory, loggerFactory, _server.Id);
        _clientTwo = CreateClient(rules, mechFactory, loggerFactory, _server.Id);
        _playerOne = new Player(Guid.NewGuid(), "Alpha", PlayerControlType.Human);
        _playerTwo = new Player(Guid.NewGuid(), "Bravo", PlayerControlType.Human);
    }

    [Fact]
    public async Task TwoClientsJoin_MapAndPhaseAreSynchronizedThroughTransport()
    {
        // Join requests travel client -> transport -> server, then the server's
        // authoritative join broadcast travels back through transport -> clients.
        (await _clientOne.JoinGameWithUnits(_playerOne, [_unitData], [])).ShouldBeTrue();
        (await _clientTwo.JoinGameWithUnits(_playerTwo, [_unitData], [])).ShouldBeTrue();

        _server.Players.Count.ShouldBe(2);
        await WaitUntil(() => _clientOne.Players.Count == 2 && _clientTwo.Players.Count == 2);

        var map = new BattleMapFactory().GenerateMap(8, 8,
            new SingleTerrainGenerator(8, 8, new ClearTerrain()));
        _server.SetBattleMap(map);

        await WaitUntil(() => _clientOne.BattleMap is not null && _clientTwo.BattleMap is not null);
        _clientOne.BattleMap.ShouldNotBeNull();
        _clientTwo.BattleMap.ShouldNotBeNull();
        _clientOne.BattleMap!.Width.ShouldBe(map.Width);
        _clientTwo.BattleMap!.Height.ShouldBe(map.Height);

        (await _clientOne.SetPlayerReady(new UpdatePlayerStatusCommand
        {
            GameOriginId = _clientOne.Id,
            PlayerId = _playerOne.Id,
            PlayerStatus = PlayerStatus.Ready
        })).ShouldBeTrue();
        (await _clientTwo.SetPlayerReady(new UpdatePlayerStatusCommand
        {
            GameOriginId = _clientTwo.Id,
            PlayerId = _playerTwo.Id,
            PlayerStatus = PlayerStatus.Ready
        })).ShouldBeTrue();

        _server.TryStartGame();

        _server.TurnPhase.ShouldBe(PhaseNames.Deployment);
        await WaitUntil(() => _clientOne.TurnPhase == PhaseNames.Deployment &&
                             _clientTwo.TurnPhase == PhaseNames.Deployment);
    }

    [Fact]
    public async Task DeploymentCommandsTravelThroughTransport_AndAdvanceToInitiative()
    {
        await JoinReadyAndStart();

        for (var index = 0; index < 2; index++)
        {
            var activePlayerId = _server.PhaseStepState!.Value.ActivePlayer.Id;
            var activeServerUnit = _server.Players.Single(p => p.Id == activePlayerId).Units.Single();
            var activeClient = activePlayerId == _playerOne.Id ? _clientOne : _clientTwo;
            await WaitUntil(() => activeClient.PhaseStepState?.ActivePlayer.Id == activePlayerId &&
                                 activeClient.CanActivePlayerAct);
            var command = new DeployUnitCommand
            {
                GameOriginId = activeClient.Id,
                PlayerId = activePlayerId,
                UnitId = activeServerUnit.Id,
                Position = new HexCoordinateData(index + 1, index + 1),
                Direction = 0
            };

            (await activeClient.DeployUnit(command)).ShouldBeTrue();
            await WaitUntil(() => _server.Players.Single(p => p.Id == activePlayerId).Units.Single().IsDeployed);
        }

        await WaitUntil(() => _server.TurnPhase != PhaseNames.Deployment,
            $"phase={_server.TurnPhase}; active={_server.PhaseStepState?.ActivePlayer.Id}; " +
            string.Join(", ", _server.CommandLog.Select(command => command.GetType().Name)));
        _server.TurnPhase.ShouldNotBe(PhaseNames.Deployment);
        _clientOne.TurnPhase.ShouldNotBe(PhaseNames.Deployment);
        _clientTwo.TurnPhase.ShouldNotBe(PhaseNames.Deployment);
        _server.Players.SelectMany(p => p.Units).ShouldAllBe(u => u.IsDeployed);
    }

    [Fact]
    public async Task PhysicalAttackTravelsThroughTransport_AndAppliesDamageToBothClients()
    {
        await JoinReadyAndStart();

        for (var index = 0; index < 2; index++)
        {
            var activePlayerId = _server.PhaseStepState!.Value.ActivePlayer.Id;
            var activeServerUnit = _server.Players.Single(p => p.Id == activePlayerId).Units.Single();
            var activeClient = activePlayerId == _playerOne.Id ? _clientOne : _clientTwo;
            await WaitUntil(() => activeClient.CanActivePlayerAct);

            (await activeClient.DeployUnit(new DeployUnitCommand
            {
                GameOriginId = activeClient.Id,
                PlayerId = activePlayerId,
                UnitId = activeServerUnit.Id,
                Position = index == 0 ? new HexCoordinateData(1, 1) : new HexCoordinateData(1, 2),
                Direction = 0
            })).ShouldBeTrue();
            await WaitUntil(() => activeServerUnit.IsDeployed);
        }

        await WaitUntil(() => _server.TurnPhase == PhaseNames.Movement);
        for (var index = 0; index < 2; index++)
        {
            var activePlayerId = _server.PhaseStepState!.Value.ActivePlayer.Id;
            var activeServerUnit = _server.Players.Single(p => p.Id == activePlayerId).Units.Single();
            var activeClient = activePlayerId == _playerOne.Id ? _clientOne : _clientTwo;
            await WaitUntil(() => activeClient.CanActivePlayerAct);
            (await activeClient.MoveUnit(new MoveUnitCommand
            {
                GameOriginId = activeClient.Id,
                PlayerId = activePlayerId,
                UnitId = activeServerUnit.Id,
                MovementType = MovementType.StandingStill,
                MovementPath = MovementPath.CreateSingleSegmentPath(activeServerUnit.Position!).ToData(),
                IsCompleted = true
            })).ShouldBeTrue();
        }

        await WaitUntil(() => _server.TurnPhase == PhaseNames.WeaponsAttack);
        for (var index = 0; index < 2; index++)
        {
            var activePlayerId = _server.PhaseStepState!.Value.ActivePlayer.Id;
            var activeClient = activePlayerId == _playerOne.Id ? _clientOne : _clientTwo;
            var activeUnit = _server.Players.Single(p => p.Id == activePlayerId).Units.Single();
            await WaitUntil(() => activeClient.CanActivePlayerAct);
            (await activeClient.DeclareWeaponAttack(new WeaponAttackDeclarationCommand
            {
                GameOriginId = activeClient.Id,
                PlayerId = activePlayerId,
                UnitId = activeUnit.Id,
                WeaponTargets = []
            })).ShouldBeTrue();
        }

        await WaitUntil(() => _server.TurnPhase == PhaseNames.PhysicalAttack);
        var attackerPlayerId = _server.PhaseStepState!.Value.ActivePlayer.Id;
        var attackerClient = attackerPlayerId == _playerOne.Id ? _clientOne : _clientTwo;
        var attacker = _server.Players.Single(p => p.Id == attackerPlayerId).Units.Single();
        var target = _server.Players.Single(p => p.Id != attackerPlayerId).Units.Single();
        var targetClient = attackerPlayerId == _playerOne.Id ? _clientTwo : _clientOne;
        var armorBefore = target.TotalCurrentArmor;
        await WaitUntil(() => attackerClient.CanActivePlayerAct);

        var physicalAttackAccepted = await attackerClient.DeclarePhysicalAttack(new PhysicalAttackCommand
        {
            GameOriginId = attackerClient.Id,
            PlayerId = attackerPlayerId,
            UnitId = attacker.Id,
            TargetUnitId = target.Id,
            AttackType = PhysicalAttackType.Punch
        });
        var validation = new PhysicalAttackValidator().Validate(attacker, target, PhysicalAttackType.Punch);
        physicalAttackAccepted.ShouldBeTrue(
            $"{validation.Error}; server phase={_server.TurnPhase}; active={_server.PhaseStepState?.ActivePlayer.Id}; " +
            $"attacker player={attackerPlayerId}; positions={attacker.Position?.Coordinates}/{target.Position?.Coordinates}; " +
            string.Join(", ", _server.CommandLog.Select(command => command.GetType().Name)));

        await WaitUntil(() => target.TotalCurrentArmor < armorBefore);
        await WaitUntil(() => targetClient.Players.SelectMany(p => p.Units)
            .Single(unit => unit.Id == target.Id).TotalCurrentArmor < armorBefore);
        _clientOne.CommandLog.ShouldContain(command => command is PhysicalAttackResolutionCommand);
        _clientTwo.CommandLog.ShouldContain(command => command is PhysicalAttackResolutionCommand);

        var remainingPlayerId = _server.PhaseStepState!.Value.ActivePlayer.Id;
        var remainingClient = remainingPlayerId == _playerOne.Id ? _clientOne : _clientTwo;
        var remainingUnit = _server.Players.Single(p => p.Id == remainingPlayerId).Units.Single();
        await WaitUntil(() => remainingClient.CanActivePlayerAct);
        (await remainingClient.PassPhysicalAttack(new PassPhysicalAttackCommand
        {
            GameOriginId = remainingClient.Id,
            PlayerId = remainingPlayerId,
            UnitId = remainingUnit.Id
        })).ShouldBeTrue();

        // Heat is an automatic phase in the local ruleset, so observe either Heat
        // or the subsequent End phase while still proving the physical phase completed.
        await WaitUntil(() => _server.TurnPhase is PhaseNames.Heat or PhaseNames.End,
            $"phase={_server.TurnPhase}; active={_server.PhaseStepState?.ActivePlayer.Id}; " +
            string.Join(", ", _server.CommandLog.Select(command => command.GetType().Name)));
    }

    [Fact]
    public async Task InvalidPhysicalAttack_IsRejectedWithoutDamageOrTurnProgress()
    {
        await AdvanceToPhysicalAttack();

        var attackerPlayerId = _server.PhaseStepState!.Value.ActivePlayer.Id;
        var attackerClient = attackerPlayerId == _playerOne.Id ? _clientOne : _clientTwo;
        var attacker = _server.Players.Single(p => p.Id == attackerPlayerId).Units.Single();
        var armorBefore = attacker.TotalCurrentArmor;
        var activeUnitIdBefore = _server.PhaseStepState.Value.ActivePlayer.Id;

        var accepted = await attackerClient.DeclarePhysicalAttack(new PhysicalAttackCommand
        {
            GameOriginId = attackerClient.Id,
            PlayerId = attackerPlayerId,
            UnitId = attacker.Id,
            TargetUnitId = attacker.Id,
            AttackType = PhysicalAttackType.Punch
        });

        accepted.ShouldBeFalse();
        _server.TurnPhase.ShouldBe(PhaseNames.PhysicalAttack);
        _server.PhaseStepState!.Value.ActivePlayer.Id.ShouldBe(activeUnitIdBefore);
        attacker.TotalCurrentArmor.ShouldBe(armorBefore);
        _server.CommandLog.ShouldNotContain(command => command is PhysicalAttackResolutionCommand);
    }

    private async Task AdvanceToPhysicalAttack()
    {
        await JoinReadyAndStart();

        for (var index = 0; index < 2; index++)
        {
            var activePlayerId = _server.PhaseStepState!.Value.ActivePlayer.Id;
            var activeServerUnit = _server.Players.Single(p => p.Id == activePlayerId).Units.Single();
            var activeClient = activePlayerId == _playerOne.Id ? _clientOne : _clientTwo;
            await WaitUntil(() => activeClient.CanActivePlayerAct);
            (await activeClient.DeployUnit(new DeployUnitCommand
            {
                GameOriginId = activeClient.Id,
                PlayerId = activePlayerId,
                UnitId = activeServerUnit.Id,
                Position = index == 0 ? new HexCoordinateData(1, 1) : new HexCoordinateData(1, 2),
                Direction = 0
            })).ShouldBeTrue();
            await WaitUntil(() => activeServerUnit.IsDeployed);
        }

        await WaitUntil(() => _server.TurnPhase == PhaseNames.Movement);
        for (var index = 0; index < 2; index++)
        {
            var activePlayerId = _server.PhaseStepState!.Value.ActivePlayer.Id;
            var activeServerUnit = _server.Players.Single(p => p.Id == activePlayerId).Units.Single();
            var activeClient = activePlayerId == _playerOne.Id ? _clientOne : _clientTwo;
            await WaitUntil(() => activeClient.CanActivePlayerAct);
            (await activeClient.MoveUnit(new MoveUnitCommand
            {
                GameOriginId = activeClient.Id,
                PlayerId = activePlayerId,
                UnitId = activeServerUnit.Id,
                MovementType = MovementType.StandingStill,
                MovementPath = MovementPath.CreateSingleSegmentPath(activeServerUnit.Position!).ToData(),
                IsCompleted = true
            })).ShouldBeTrue();
        }

        await WaitUntil(() => _server.TurnPhase == PhaseNames.WeaponsAttack);
        for (var index = 0; index < 2; index++)
        {
            var activePlayerId = _server.PhaseStepState!.Value.ActivePlayer.Id;
            var activeClient = activePlayerId == _playerOne.Id ? _clientOne : _clientTwo;
            var activeUnit = _server.Players.Single(p => p.Id == activePlayerId).Units.Single();
            await WaitUntil(() => activeClient.CanActivePlayerAct);
            (await activeClient.DeclareWeaponAttack(new WeaponAttackDeclarationCommand
            {
                GameOriginId = activeClient.Id,
                PlayerId = activePlayerId,
                UnitId = activeUnit.Id,
                WeaponTargets = []
            })).ShouldBeTrue();
        }

        await WaitUntil(() => _server.TurnPhase == PhaseNames.PhysicalAttack);
    }

    /// <summary>Creates the minimum ready lobby state needed by the phase workflow.</summary>
    private async Task JoinReadyAndStart()
    {
        // Each player must receive a distinct unit identity; reusing the same UnitData
        // would make a physical attack appear to target the attacker itself.
        var firstUnit = _unitData with { Id = Guid.NewGuid() };
        var secondUnit = _unitData with { Id = Guid.NewGuid() };
        (await _clientOne.JoinGameWithUnits(_playerOne, [firstUnit], [])).ShouldBeTrue();
        (await _clientTwo.JoinGameWithUnits(_playerTwo, [secondUnit], [])).ShouldBeTrue();

        _server.SetBattleMap(new BattleMapFactory().GenerateMap(8, 8,
            new SingleTerrainGenerator(8, 8, new ClearTerrain())));
        await WaitUntil(() => _clientOne.BattleMap is not null && _clientTwo.BattleMap is not null);

        (await _clientOne.SetPlayerReady(new UpdatePlayerStatusCommand
        {
            GameOriginId = _clientOne.Id, PlayerId = _playerOne.Id, PlayerStatus = PlayerStatus.Ready
        })).ShouldBeTrue();
        (await _clientTwo.SetPlayerReady(new UpdatePlayerStatusCommand
        {
            GameOriginId = _clientTwo.Id, PlayerId = _playerTwo.Id, PlayerStatus = PlayerStatus.Ready
        })).ShouldBeTrue();

        _server.TryStartGame();
        await WaitUntil(() => _server.TurnPhase == PhaseNames.Deployment &&
                             _server.PhaseStepState is not null &&
                             _clientOne.TurnPhase == PhaseNames.Deployment &&
                             _clientTwo.TurnPhase == PhaseNames.Deployment);
    }

    /// <summary>Allows the transport scheduler to deliver an asynchronous message.</summary>
    private static async Task WaitUntil(Func<bool> condition, string? failureMessage = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        condition().ShouldBeTrue(failureMessage ?? "The local transport did not deliver the expected command.");
    }

    private ClientGame CreateClient(IRulesProvider rules, IMechFactory mechFactory,
        ILoggerFactory loggerFactory, Guid serverId)
    {
        return new ClientGame(rules, mechFactory, _publisher,
            Substitute.For<IToHitCalculator>(), Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(), Substitute.For<IHeatEffectsCalculator>(),
            new BattleMapFactory(), new HashService(), loggerFactory.CreateLogger<ClientGame>(), serverId,
            ackTimeoutMilliseconds: 1000);
    }

    public void Dispose()
    {
        _clientOne.Dispose();
        _clientTwo.Dispose();
        _server.Dispose();
        _adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
