using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class PhysicalAttackStateTests
{
    private static readonly HexCoordinates AttackerHex = new(5, 5);
    private static readonly IBattleMapFactory BattleMapFactory = new BattleMapFactory();

    private readonly PhysicalAttackState _sut;
    private readonly ClientGame _game;
    private readonly Player _player;
    private readonly BattleMapViewModel _viewModel;
    private readonly IPhysicalAttackCalculator _physicalAttackCalculator = Substitute.For<IPhysicalAttackCalculator>();
    private readonly ICommandPublisher _commandPublisher = Substitute.For<ICommandPublisher>();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IHashService _hashService = Substitute.For<IHashService>();
    private readonly IPlatformService _platformService = Substitute.For<IPlatformService>();
    private readonly Guid _idempotencyKey = Guid.NewGuid();

    public PhysicalAttackStateTests()
    {
        StubLocalization();

        _hashService.ComputeCommandIdempotencyKey(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Type>(), Arg.Any<int>(), Arg.Any<string>(),
                Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<string?>())
            .Returns(_idempotencyKey);

        _viewModel = new BattleMapViewModel(
            Substitute.For<IImageService>(),
            Substitute.For<ITerrainAssetService>(),
            _localizationService,
            Substitute.For<IDispatcherService>(),
            new TotalWarfareRulesProvider(),
            _platformService);

        _player = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human);

        var battleMap = BattleMapFactory.GenerateMap(
            11, 11,
            new SingleTerrainGenerator(11, 11, new ClearTerrain()));

        _game = CreateGame();

        _game.JoinGameWithUnits(_player, [], []);
        _game.SetBattleMap(battleMap);
        _viewModel.Game = _game;

        AddPlayerUnits();
        SetActivePlayer();

        _sut = new PhysicalAttackState(_viewModel);
        BindViewModelCurrentStateTo(_sut);
    }

    private void BindViewModelCurrentStateTo(PhysicalAttackState state)
    {
        // The state drives view-model properties (preview visibility) through NotifyStateChanged,
        // so the view model has to expose it as its current state.
        typeof(BattleMapViewModel).GetProperty(nameof(BattleMapViewModel.CurrentState))!
            .GetSetMethod(true)!.Invoke(_viewModel, [state]);
    }

    private ClientGame CreateGame() => new(
        new TotalWarfareRulesProvider(),
        new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService),
        _commandPublisher,
        Substitute.For<IToHitCalculator>(),
        Substitute.For<IPilotingSkillCalculator>(),
        Substitute.For<IConsciousnessCalculator>(),
        Substitute.For<IHeatEffectsCalculator>(),
        new PhysicalAttackEligibility(),
        _physicalAttackCalculator,
        Substitute.For<IBattleMapFactory>(),
        _hashService,
        Substitute.For<ILogger<ClientGame>>());

    private void StubLocalization()
    {
        _localizationService.GetString(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));
        _localizationService.GetString("Action_SelectUnitToAttack").Returns("Select unit to attack");
        _localizationService.GetString("Action_SelectAction").Returns("Select action");
        _localizationService.GetString("Action_SelectTarget").Returns("Select Target");
        _localizationService.GetString("Action_SkipAttack").Returns("Skip Attack");
        _localizationService.GetString("Action_DeclareAttack").Returns("Declare Attack");
        _localizationService.GetString("Action_DeclarePhysicalAttack_WithLimb").Returns("{0} ({1})");
        _localizationService.GetString("PhysicalAttack_BothArms").Returns("Both Arms");
        _localizationService.GetString("PhysicalAttack_NoOptions").Returns("This unit has no physical attack options");
        _localizationService.GetString("PhysicalAttackPreview_Title").Returns("Physical attack");
        _localizationService.GetString("PhysicalAttackType_Punch").Returns("Punch");
        _localizationService.GetString("PhysicalAttackType_Kick").Returns("Kick");
        _localizationService.GetString("Attack_TargetNumber").Returns("Target ToHit Number");
        _localizationService.GetString("Attack_ImpossibleToHit").Returns("Impossible to hit");
        _localizationService.GetString("Modifier_PilotingSkill").Returns("Piloting Skill: +{0}");
        _localizationService.GetString("Modifier_PhysicalAttackBase").Returns("{0} Base: {1:+#;-#;0}");
        _localizationService.GetString("Modifier_AttackerMovement").Returns("Attacker Movement ({0}): +{1}");
        _localizationService.GetString("Modifier_TargetMovement").Returns("Target Movement ({0} hexes): +{1}");
        _localizationService.GetString("MechPart_LeftArm_Short").Returns("LA");
        _localizationService.GetString("MechPart_RightArm_Short").Returns("RA");
        _localizationService.GetString("MechPart_LeftLeg_Short").Returns("LL");
        _localizationService.GetString("MechPart_RightLeg_Short").Returns("RL");
    }

    private void AddPlayerUnits()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var enemyId = Guid.NewGuid();

        _game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player1",
            Units = [unitData, unitData],
            Tint = "#FF0000",
            GameOriginId = Guid.NewGuid(),
            PlayerId = _player.Id,
            PilotAssignments = [],
            IdempotencyKey = _idempotencyKey
        });
        _game.HandleCommand(new JoinGameCommand
        {
            PlayerName = "Player2",
            Units = [unitData, unitData],
            Tint = "#FFFF00",
            GameOriginId = Guid.NewGuid(),
            PlayerId = enemyId,
            PilotAssignments = []
        });

        foreach (var unit in _game.Players.SelectMany(p => p.Units))
        {
            var pilot = Substitute.For<IPilot>();
            pilot.IsConscious.Returns(true);
            unit.AssignPilot(pilot);
        }
    }

    private void SetActivePlayer()
    {
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = _player.Id,
            UnitsToPlay = 1
        });
    }

    private IUnit Attacker => _viewModel.Units.First(unit => unit.Owner!.Id == _player.Id);

    private IUnit Enemy => _viewModel.Units.First(unit => unit.Owner!.Id != _player.Id);

    private IUnit SecondEnemy => _viewModel.Units.Where(unit => unit.Owner!.Id != _player.Id).ElementAt(1);

    private void DeployPair(HexCoordinates? enemyHex = null)
    {
        Attacker.Deploy(new HexPosition(AttackerHex, HexDirection.Top), null);
        Enemy.Deploy(new HexPosition(enemyHex ?? AttackerHex.GetNeighbour(HexDirection.Top), HexDirection.Bottom), null);
    }

    private void SwitchActivePlayerToEnemy()
    {
        var enemyPlayerId = _game.Players.First(player => player.Id != _player.Id).Id;
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = enemyPlayerId,
            UnitsToPlay = 1
        });
    }

    private static void Destroy(IUnit unit)
    {
        var centerTorso = unit.Parts[PartLocation.CenterTorso];
        centerTorso.ApplyDamage(centerTorso.CurrentArmor + centerTorso.CurrentStructure + 10, HitDirection.Front);
        unit.UpdateDestroyedStatus();
    }

    private void StubCalculator(int targetNumber = 7)
    {
        _physicalAttackCalculator
            .GetToHitNumber(Arg.Any<IUnit>(), Arg.Any<IUnit>(), Arg.Any<PhysicalAttackType>(), Arg.Any<IBattleMap>())
            .Returns(targetNumber);
        _physicalAttackCalculator
            .GetModifierBreakdown(Arg.Any<IUnit>(), Arg.Any<IUnit>(), Arg.Any<PhysicalAttackType>(), Arg.Any<IBattleMap>())
            .Returns(CreateBreakdown());
    }

    private static PhysicalAttackBreakdown CreateBreakdown() => new()
    {
        PilotingBase = new PilotingRollModifier { Value = 5 },
        AttackTypeModifier = new PhysicalAttackBaseModifier
        {
            Value = 0,
            AttackType = PhysicalAttackType.Punch
        },
        AttackerMovement = new AttackerMovementModifier
        {
            Value = 0,
            MovementType = MovementType.StandingStill
        },
        TargetMovement = new TargetMovementModifier { Value = 0, HexesMoved = 0 },
        TerrainModifiers = [],
        OtherModifiers = []
    };

    private void SelectAttacker() => _sut.HandleHexSelection(new Hex(AttackerHex));

    private StateAction GetAction(string label) =>
        _sut.GetAvailableActions().Single(action => action.Label == label);

    [Fact]
    public void InitialState_IsSelectingUnit()
    {
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.SelectingUnit);
        _sut.Attacker.ShouldBeNull();
        _sut.ActionLabel.ShouldBe("Select unit to attack");
        _sut.GetAvailableActions().ShouldBeEmpty();
    }

    [Fact]
    public void SelectingAnAttacker_TransitionsToActionSelection()
    {
        DeployPair();

        SelectAttacker();

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.ActionSelection);
        _sut.Attacker.ShouldBe(Attacker);
        _sut.ActionLabel.ShouldBe("Select action");
    }

    [Fact]
    public void FrontArcTarget_OffersPunchAndKick()
    {
        DeployPair();
        SelectAttacker();

        var labels = _sut.GetAvailableActions().Select(a => a.Label).ToList();

        labels.ShouldContain("Punch");
        labels.ShouldContain("Kick");
        labels.ShouldContain("Skip Attack");
    }

    [Fact]
    public void SideArcTarget_OffersPunchOnly()
    {
        DeployPair(AttackerHex.GetNeighbour(HexDirection.BottomRight));
        SelectAttacker();

        var labels = _sut.GetAvailableActions().Select(a => a.Label).ToList();

        labels.ShouldContain("Punch");
        labels.ShouldNotContain("Kick");
    }

    [Fact]
    public void SelectingAnAttackType_HighlightsTheValidTargetHex()
    {
        DeployPair();
        SelectAttacker();

        GetAction("Punch").OnExecute();

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
        var targetHex = _game.BattleMap!.GetHex(Enemy.Position!.Coordinates);
        targetHex!.Highlights.OfType<AttackReachableHighlight>().ShouldNotBeEmpty();
    }

    /// <summary>
    /// Selecting an attacker highlights every hex reachable by any attack type, so choosing a
    /// single type has to drop the highlights that only the other types can reach.
    /// </summary>
    [Fact]
    public void SelectingAnAttackType_ClearsHighlightsThatOnlyOtherAttackTypesCanReach()
    {
        // Arrange: a punchable enemy in the front arc and a kick-only one a level below,
        // because a punch cannot reach a lower target while a kick can.
        var punchableHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var kickOnlyHex = AttackerHex.GetNeighbour(HexDirection.TopRight);
        var kickOnlyLevel = new Hex(kickOnlyHex, -1);
        kickOnlyLevel.AddTerrain(new ClearTerrain());
        _game.BattleMap!.AddHex(kickOnlyLevel);
        DeployPair(punchableHex);
        SecondEnemy.Deploy(new HexPosition(kickOnlyHex, HexDirection.Bottom), null);

        SelectAttacker();
        _game.BattleMap.GetHex(kickOnlyHex)!.Highlights.OfType<AttackReachableHighlight>()
            .ShouldNotBeEmpty("the attacker-wide highlight covers the kick-only hex");

        // Act
        GetAction("Punch").OnExecute();

        // Assert
        _game.BattleMap.GetHex(kickOnlyHex)!.Highlights.ShouldBeEmpty();
        var punchableHighlight = _game.BattleMap.GetHex(punchableHex)!
            .Highlights.OfType<AttackReachableHighlight>().Single();
        punchableHighlight.WeaponNames.ShouldBe(["Punch"]);
    }

    [Fact]
    public void SelectingTheTarget_ShowsThePreview()
    {
        DeployPair();
        StubCalculator(8);
        SelectAttacker();
        GetAction("Punch").OnExecute();

        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));

        _sut.SelectedTarget.ShouldBe(Enemy);
        _sut.Preview.ShouldNotBeNull();
        _sut.Preview!.TargetNumber.ShouldBe(8);
        _sut.Preview.AttackTypeName.ShouldBe("Punch");
        _sut.Preview.TargetName.ShouldBe(Enemy.Name);
        _sut.Preview.TargetNumberText.ShouldBe("Target ToHit Number: 8");
        _sut.Preview.ModifierLines.ShouldContain("Piloting Skill: +5");
        _viewModel.IsPhysicalAttackPreviewVisible.ShouldBeTrue();
    }

    [Fact]
    public void Preview_ReportsImpossibleAttacks()
    {
        DeployPair();
        StubCalculator(13);
        SelectAttacker();
        GetAction("Punch").OnExecute();

        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));

        _sut.Preview!.IsImpossible.ShouldBeTrue();
        _sut.PlayerActionLabel.ShouldBe("Declare Attack");
    }

    [Fact]
    public void ClickingAnInvalidHex_CancelsTargetSelectionAndClearsHighlights()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();
        GetAction("Punch").OnExecute();
        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));

        _sut.HandleHexSelection(new Hex(new HexCoordinates(1, 1)));

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.ActionSelection);
        _sut.SelectedTarget.ShouldBeNull();
        _sut.Preview.ShouldBeNull();
        _viewModel.IsPhysicalAttackPreviewVisible.ShouldBeFalse();
        Enemy.Position!.Coordinates
            .ShouldSatisfyAllConditions(coordinates =>
                _game.BattleMap!.GetHex(coordinates)!.Highlights.ShouldBeEmpty());
    }

    [Fact]
    public void DeclaringWithOneArm_PublishesThatLimb()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();
        GetAction("Punch").OnExecute();
        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));

        GetAction("Punch (LA)").OnExecute();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration != null &&
            command.Declaration.AttackType == PhysicalAttackType.Punch &&
            command.Declaration.AttackerLimbs.SequenceEqual(new[] { PartLocation.LeftArm }) &&
            command.Declaration.TargetId == Enemy.Id &&
            command.UnitId == Attacker.Id));
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.SelectingUnit);
        _sut.Attacker.ShouldBeNull();
    }

    [Fact]
    public void DeclaringWithBothArms_PublishesBothLimbs()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();
        GetAction("Punch").OnExecute();
        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));

        GetAction("Punch (Both Arms)").OnExecute();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration != null &&
            command.Declaration.AttackerLimbs.Count == 2));
    }

    [Fact]
    public void DeclaringAKick_PublishesOneLeg()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();
        GetAction("Kick").OnExecute();
        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));

        GetAction("Kick (LL)").OnExecute();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration != null &&
            command.Declaration.AttackType == PhysicalAttackType.Kick &&
            command.Declaration.AttackerLimbs.Count == 1 &&
            command.Declaration.AttackerLimbs[0] == PartLocation.LeftLeg));
    }

    [Fact]
    public void PlayerAction_DeclaresTheDefaultOption()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();
        GetAction("Punch").OnExecute();
        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));

        _sut.PlayerActionLabel.ShouldBe("Declare Attack");
        _sut.ExecutePlayerAction();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration != null && command.Declaration.AttackerLimbs.Count == 2));
    }

    [Fact]
    public void PlayerAction_InActionSelection_Skips()
    {
        DeployPair();
        SelectAttacker();

        _sut.PlayerActionLabel.ShouldBe("Skip Attack");
        _sut.ExecutePlayerAction();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration == null && command.UnitId == Attacker.Id));
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.SelectingUnit);
    }

    [Fact]
    public void SkipAction_PublishesANullDeclaration()
    {
        DeployPair();
        SelectAttacker();

        GetAction("Skip Attack").OnExecute();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration == null && command.UnitId == Attacker.Id));
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.SelectingUnit);
    }

    [Fact]
    public void EnemyUnitsAndUnitsThatAlreadyDeclared_CannotBeSelectedAsAttackers()
    {
        DeployPair();

        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));

        _sut.Attacker.ShouldBeNull();

        Attacker.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Punch,
            AttackerLimbs = [PartLocation.LeftArm],
            TargetId = Enemy.Id
        });
        _sut.HandleHexSelection(new Hex(Attacker.Position!.Coordinates));

        _sut.Attacker.ShouldBeNull();
    }

    [Fact]
    public void DestroyedAttacker_CannotBeSelected()
    {
        DeployPair();
        var attacker = Attacker;
        var centerTorso = attacker.Parts[PartLocation.CenterTorso];
        centerTorso.ApplyDamage(centerTorso.CurrentArmor + centerTorso.CurrentStructure + 10, HitDirection.Front);
        attacker.UpdateDestroyedStatus();
        attacker.IsDestroyed.ShouldBeTrue();

        _sut.HandleHexSelection(new Hex(attacker.Position!.Coordinates));

        _sut.Attacker.ShouldBeNull();
    }

    [Fact]
    public void LostPermission_BlocksDeclarations()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();
        GetAction("Punch").OnExecute();
        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));

        var enemyPlayerId = _game.Players.First(player => player.Id != _player.Id).Id;
        _game.HandleCommand(new ChangeActivePlayerCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = enemyPlayerId,
            UnitsToPlay = 1
        });

        _sut.ExecutePlayerAction();

        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<PhysicalAttackCommand>());
    }

    [Fact]
    public void MobileActionList_ExposesTheDeclarationOptions()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();
        GetAction("Punch").OnExecute();
        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));

        var actions = _sut.GetAvailableActions().ToList();

        actions.Select(action => action.Label).ShouldBe(
            ["Punch (LA)", "Punch (RA)", "Punch (Both Arms)", "Skip Attack"]);
        actions.ShouldAllBe(action => action.IsVisible);
    }

    [Fact]
    public void SwitchingAttacker_ResetsThePreviousSelection()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();
        GetAction("Punch").OnExecute();
        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));
        _sut.Preview.ShouldNotBeNull();

        // Abandon target selection first: it locks selection to the target hex.
        _sut.HandleHexSelection(new Hex(new HexCoordinates(1, 1)));

        var secondAttacker = _viewModel.Units.Last(unit => unit.Owner!.Id == _player.Id);
        secondAttacker.Deploy(new HexPosition(AttackerHex.GetNeighbour(HexDirection.TopRight), HexDirection.Top), null);
        _sut.HandleHexSelection(new Hex(secondAttacker.Position!.Coordinates));

        _sut.Attacker.ShouldBe(secondAttacker);
        _sut.SelectedTarget.ShouldBeNull();
        _sut.Preview.ShouldBeNull();
        _viewModel.IsPhysicalAttackPreviewVisible.ShouldBeFalse();
    }

    [Fact]
    public void SelectingAnAttacker_ExposesTheAvailableAttackTypes()
    {
        DeployPair();

        SelectAttacker();

        _sut.AvailableAttackTypes.ShouldContain(PhysicalAttackType.Punch);
        _sut.AvailableAttackTypes.ShouldContain(PhysicalAttackType.Kick);
        _sut.SelectedAttackType.ShouldBeNull();
    }

    [Fact]
    public void SelectingAnAttackType_ExposesTheSelectedType()
    {
        DeployPair();
        SelectAttacker();

        GetAction("Punch").OnExecute();

        _sut.SelectedAttackType.ShouldBe(PhysicalAttackType.Punch);
    }

    [Fact]
    public void TargetSelection_PromptsForTargetAndSelectsTheTargetUnit()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();
        GetAction("Punch").OnExecute();

        _sut.ActionLabel.ShouldBe("Select Target");
        _sut.SelectedUnit.ShouldBeNull();
        _sut.PlayerActionLabel.ShouldBe("Skip Attack");
        _sut.GetAvailableActions().Select(action => action.Label).ShouldBe(["Skip Attack"]);

        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));

        _sut.SelectedUnit.ShouldBe(Enemy);
    }

    [Fact]
    public void CanExecutePlayerAction_RequiresActionOrTargetSelectionAndAVisibleActionMenu()
    {
        DeployPair();
        _sut.CanExecutePlayerAction.ShouldBeFalse();

        SelectAttacker();
        _sut.CanExecutePlayerAction.ShouldBeTrue();

        GetAction("Punch").OnExecute();
        _sut.CanExecutePlayerAction.ShouldBeTrue();

        _platformService.IsMobile.Returns(true);
        _sut.CanExecutePlayerAction.ShouldBeFalse();
    }

    [Fact]
    public void PlayerActionLabel_IsEmptyWhileSelectingAnAttacker()
    {
        DeployPair();

        _sut.IsActionRequired.ShouldBeTrue();
        _sut.PlayerActionLabel.ShouldBeEmpty();
    }

    [Fact]
    public void PlayerActionLabel_IsEmptyWhenTheActivePlayerCannotAct()
    {
        DeployPair();
        SelectAttacker();
        SwitchActivePlayerToEnemy();

        _sut.IsActionRequired.ShouldBeFalse();
        _sut.PlayerActionLabel.ShouldBeEmpty();
    }

    [Fact]
    public void PlayerAction_DuringTargetSelectionWithoutATarget_Skips()
    {
        DeployPair();
        SelectAttacker();
        GetAction("Punch").OnExecute();

        _sut.ExecutePlayerAction();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration == null && command.UnitId == Attacker.Id));
    }

    [Fact]
    public void HandleUnitSelectionFromList_SelectsAnAttacker()
    {
        DeployPair();

        _sut.HandleUnitSelectionFromList(Attacker);

        _sut.Attacker.ShouldBe(Attacker);
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.ActionSelection);
    }

    [Fact]
    public void HandleUnitSelectionFromList_DuringTargetSelectionWithAnIneligibleUnit_CancelsSelection()
    {
        DeployPair();
        SelectAttacker();
        GetAction("Punch").OnExecute();

        _sut.HandleUnitSelectionFromList(Attacker);

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.ActionSelection);
        _sut.SelectedTarget.ShouldBeNull();
        _sut.SelectedAttackType.ShouldBeNull();
    }

    [Fact]
    public void UnitSelectionAfterLosingPermission_IsIgnored()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();
        GetAction("Punch").OnExecute();
        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));
        SwitchActivePlayerToEnemy();

        _sut.HandleUnitSelectionFromList(Attacker);

        _sut.SelectedTarget.ShouldBe(Enemy);
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
    }

    [Fact]
    public void HandleFacingSelection_DoesNotChangeTheState()
    {
        DeployPair();
        SelectAttacker();

        _sut.HandleFacingSelection(HexDirection.Top);

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.ActionSelection);
        _sut.Attacker.ShouldBe(Attacker);
    }

    [Fact]
    public void CanSelectUnit_AllowsOnlyOwnUndeclaredUnitsBeforeTargetSelection()
    {
        DeployPair();

        _sut.CanSelectUnit(null).ShouldBeTrue();
        _sut.CanSelectUnit(Attacker).ShouldBeTrue();
        _sut.CanSelectUnit(Enemy).ShouldBeFalse();

        SelectAttacker();
        GetAction("Punch").OnExecute();

        _sut.CanSelectUnit(Attacker).ShouldBeFalse();
    }

    [Fact]
    public void UnitWithoutAnAdjacentEnemy_CannotBeSelectedAsAttacker()
    {
        Attacker.Deploy(new HexPosition(AttackerHex, HexDirection.Top), null);
        Enemy.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top), null);

        _sut.CanSelectUnit(Attacker).ShouldBeFalse();
        _sut.HandleHexSelection(new Hex(AttackerHex));

        _sut.Attacker.ShouldBeNull();
    }

    [Fact]
    public void SelectingAnAttackTypeWithoutRemainingTargets_StaysInActionSelection()
    {
        DeployPair();
        SelectAttacker();
        Destroy(Enemy);

        GetAction("Punch").OnExecute();

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.ActionSelection);
        _sut.SelectedAttackType.ShouldBeNull();
        _sut.SelectedTarget.ShouldBeNull();
    }

    [Fact]
    public void StaleDeclarationAction_AfterTargetSelectionIsCancelled_DoesNothing()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();
        GetAction("Punch").OnExecute();
        _sut.HandleHexSelection(new Hex(Enemy.Position!.Coordinates));
        var declaration = GetAction("Punch (LA)");

        // Cancelling target selection clears the target and the attack type; a stale action
        // captured by the view must not publish anything.
        _sut.HandleHexSelection(new Hex(new HexCoordinates(1, 1)));
        declaration.OnExecute();

        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<PhysicalAttackCommand>());
    }

    [Fact]
    public void Constructor_WithoutGame_Throws()
    {
        _viewModel.Game = null;

        var exception = Should.Throw<InvalidOperationException>(() => new PhysicalAttackState(_viewModel));

        exception.Message.ShouldBe("Game is not client game");
    }

    [Fact]
    public void Constructor_WithoutActivePlayer_Throws()
    {
        _viewModel.Game = CreateGame();

        var exception = Should.Throw<InvalidOperationException>(() => new PhysicalAttackState(_viewModel));

        exception.Message.ShouldBe("Active player is null");
    }
}
