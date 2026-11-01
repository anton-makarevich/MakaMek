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
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
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
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
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
        // The state drives view-model properties (dialog visibility) through NotifyStateChanged,
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
        _localizationService.GetString("Action_SelectTarget").Returns("Select Target");
        _localizationService.GetString("Action_SelectPhysicalAttack").Returns("Select attack");
        _localizationService.GetString("Action_SkipAttack").Returns("Skip Attack");
        _localizationService.GetString("Action_DeclareAttack").Returns("Declare Attack");
        _localizationService.GetString("Action_DeclarePhysicalAttack_WithLimb").Returns("{0} ({1})");
        _localizationService.GetString("PhysicalAttack_BothArms").Returns("Both Arms");
        _localizationService.GetString("PhysicalAttack_NoOptions").Returns("This unit has no physical attack options");
        _localizationService.GetString("PhysicalAttackDeclaration_Title").Returns("Physical attack");
        _localizationService.GetString("PhysicalAttackDeclaration_Unavailable").Returns("Not available");
        _localizationService.GetString("PhysicalAttackType_Punch").Returns("Punch");
        _localizationService.GetString("PhysicalAttackType_Kick").Returns("Kick");
        _localizationService.GetString("PhysicalAttackType_Push").Returns("Push");
        _localizationService.GetString("PhysicalAttackRestriction_NoTargetInFront")
            .Returns("No unit directly in front");
        _localizationService.GetString("PhysicalAttackRestriction_TargetNotStandingMech")
            .Returns("The target must be a standing Mech");
        _localizationService.GetString("PhysicalAttackRestriction_TargetPerformingDisplacementAttack")
            .Returns("The target is performing a Charge or Death From Above");
        _localizationService.GetString("PhysicalAttackRestriction_ElevationMismatch")
            .Returns("The target must be at the same elevation");
        _localizationService.GetString("PhysicalAttackRestriction_ArmMissing")
            .Returns("Both arms are required");
        _localizationService.GetString("PhysicalAttackRestriction_ArmWeaponFired")
            .Returns("An arm-mounted weapon was fired this turn");
        _localizationService.GetString("PhysicalAttackRestriction_TargetAlreadyDisplaced")
            .Returns("The target is already being displaced this turn");
        _localizationService.GetString("PhysicalAttackRestriction_TargetOutsideArc")
            .Returns("The target is outside the attack arc");
        _localizationService.GetString("PhysicalAttackRestriction_LegDestroyed")
            .Returns("A kicking leg is destroyed");
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
        _localizationService.GetString("MechPart_LeftArm").Returns("Left Arm");
        _localizationService.GetString("MechPart_RightArm").Returns("Right Arm");
        _localizationService.GetString("MechPart_LeftLeg").Returns("Left Leg");
        _localizationService.GetString("MechPart_RightLeg").Returns("Right Leg");
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

    private IUnit SecondAttacker => _viewModel.Units.Last(unit => unit.Owner!.Id == _player.Id);

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

    private void SelectTarget(IUnit target) =>
        _sut.HandleHexSelection(new Hex(target.Position!.Coordinates));

    private void OpenDialog(HexCoordinates? enemyHex = null, int targetNumber = 7)
    {
        DeployPair(enemyHex);
        StubCalculator(targetNumber);
        SelectAttacker();
        SelectTarget(Enemy);
    }

    private PhysicalAttackOptionViewModel Option(PhysicalAttackType attackType) =>
        _sut.Options.Single(option => option.AttackType == attackType);

    private void SelectRow(PhysicalAttackType attackType)
    {
        var option = Option(attackType);
        var label = attackType switch
        {
            PhysicalAttackType.Punch => "Both Arms",
            PhysicalAttackType.Kick => "Left Leg",
            PhysicalAttackType.Push => "Both Arms",
            _ => option.LimbOptions[0].Label
        };
        option.LimbOptions.Single(limbOption => limbOption.Label == label).IsSelected = true;
    }

    private void ChooseVariant(PhysicalAttackType attackType, string label) =>
        Option(attackType).LimbOptions.Single(limbOption => limbOption.Label == label).IsSelected = true;

    private StateAction GetAction(string label) =>
        _sut.GetAvailableActions().Single(action => action.Label == label);

    [Fact]
    public void InitialState_IsSelectingUnit()
    {
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.SelectingUnit);
        _sut.Attacker.ShouldBeNull();
        _sut.ActionLabel.ShouldBe("Select unit to attack");
        _sut.GetAvailableActions().ShouldBeEmpty();
        _sut.Options.ShouldBeEmpty();
    }

    [Fact]
    public void SelectingAnAttacker_GoesStraightToTargetSelection()
    {
        DeployPair();

        SelectAttacker();

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
        _sut.Attacker.ShouldBe(Attacker);
        _sut.ActionLabel.ShouldBe("Select Target");
        _sut.Options.ShouldBeEmpty();
        _sut.GetAvailableActions().Select(action => action.Label).ShouldBe(["Skip Attack"]);
        _viewModel.IsPhysicalAttackDeclarationVisible.ShouldBeFalse();
    }

    [Fact]
    public void SelectingAnAttacker_HighlightsEveryReachableTarget()
    {
        var punchableHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var kickOnlyHex = AttackerHex.GetNeighbour(HexDirection.TopRight);
        var kickOnlyLevel = new Hex(kickOnlyHex, -1);
        kickOnlyLevel.AddTerrain(new ClearTerrain());
        _game.BattleMap!.AddHex(kickOnlyLevel);
        DeployPair(punchableHex);
        SecondEnemy.Deploy(new HexPosition(kickOnlyHex, HexDirection.Bottom), null);

        SelectAttacker();

        _game.BattleMap.GetHex(punchableHex)!.Highlights.OfType<AttackReachableHighlight>()
            .ShouldNotBeEmpty();
        var kickOnlyHighlight = _game.BattleMap.GetHex(kickOnlyHex)!
            .Highlights.OfType<AttackReachableHighlight>().Single();
        kickOnlyHighlight.WeaponNames.ShouldBe(["Kick"]);
    }

    [Fact]
    public void SelectingTheTarget_OpensTheDeclarationDialogWithOneRowPerSupportedType()
    {
        OpenDialog();

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.Declaration);
        _sut.SelectedTarget.ShouldBe(Enemy);
        _sut.Options.Select(option => option.AttackType).ShouldBe(
            [PhysicalAttackType.Punch, PhysicalAttackType.Kick, PhysicalAttackType.Push]);
        _viewModel.IsPhysicalAttackDeclarationVisible.ShouldBeTrue();
        _viewModel.PhysicalAttackTarget.ShouldBe(Enemy);
        _viewModel.PhysicalAttackOptions.ShouldBe(_sut.Options);
    }

    [Fact]
    public void Dialog_ForAFrontArcTarget_EnablesPunchAndKickAndPush()
    {
        OpenDialog();

        Option(PhysicalAttackType.Punch).IsEnabled.ShouldBeTrue();
        Option(PhysicalAttackType.Kick).IsEnabled.ShouldBeTrue();
        Option(PhysicalAttackType.Push).IsEnabled.ShouldBeTrue();
        Option(PhysicalAttackType.Punch).RestrictionReason.ShouldBeNull();
    }

    [Fact]
    public void Dialog_ForASideArcTarget_EnablesPunchWithTheReachingArmAndDisablesKick()
    {
        OpenDialog(AttackerHex.GetNeighbour(HexDirection.BottomRight));

        var punch = Option(PhysicalAttackType.Punch);
        punch.IsEnabled.ShouldBeTrue();
        punch.LimbOptions.Count.ShouldBe(3);
        var rightArm = punch.LimbOptions.Single(option => option.Label == "Right Arm");
        rightArm.Limbs.ShouldBe([PartLocation.RightArm]);
        rightArm.IsEnabled.ShouldBeTrue();
        rightArm.RestrictionReason.ShouldBeNull();
        var leftArm = punch.LimbOptions.Single(option => option.Label == "Left Arm");
        leftArm.IsEnabled.ShouldBeFalse();
        leftArm.RestrictionReason.ShouldBe("The target is outside the attack arc");
        punch.LimbOptions.Single(option => option.Label == "Both Arms").IsEnabled.ShouldBeFalse();

        var kick = Option(PhysicalAttackType.Kick);
        kick.IsEnabled.ShouldBeFalse();
        kick.RestrictionReason.ShouldBe("The target is outside the attack arc");
        kick.LimbOptions.Count.ShouldBe(2);
        kick.LimbOptions.ShouldAllBe(option => !option.IsEnabled);
        kick.LimbOptions.ShouldAllBe(option =>
            option.RestrictionReason == "The target is outside the attack arc");
    }

    [Fact]
    public void Dialog_UsesTheLimbNamesForEveryOption()
    {
        OpenDialog();

        Option(PhysicalAttackType.Punch).LimbOptions.Select(limbOption => limbOption.Label)
            .ShouldBe(["Left Arm", "Right Arm", "Both Arms"]);
        Option(PhysicalAttackType.Kick).LimbOptions.Select(limbOption => limbOption.Label)
            .ShouldBe(["Left Leg", "Right Leg"]);
        Option(PhysicalAttackType.Push).LimbOptions.Select(limbOption => limbOption.Label)
            .ShouldBe(["Both Arms"]);
    }

    [Fact]
    public void Dialog_StartsWithNoLimbSelected()
    {
        OpenDialog();

        foreach (var attackType in new[]
                 {
                     PhysicalAttackType.Punch, PhysicalAttackType.Kick, PhysicalAttackType.Push
                 })
        {
            var option = Option(attackType);
            option.IsSelected.ShouldBeFalse();
            option.SelectedVariant.ShouldBeNull();
            option.LimbOptions.ShouldAllBe(limbOption => !limbOption.IsSelected);
        }
    }

    [Fact]
    public void Dialog_ExposesTheTargetNumberAndProbabilityForEnabledRows()
    {
        OpenDialog(targetNumber: 8);

        var punch = Option(PhysicalAttackType.Punch);
        punch.TargetNumber.ShouldBe(8);
        punch.TargetNumberText.ShouldBe("Target ToHit Number: 8");
        punch.HitProbabilityText.ShouldNotBe("-");
        punch.AttackPossibilityDescription.ShouldContain("Piloting Skill: +5");
    }

    [Fact]
    public void Dialog_ExposesTheDamageOfEachLimbCombination()
    {
        _physicalAttackCalculator
            .GetDamage(Arg.Any<IUnit>(), PhysicalAttackType.Punch, Arg.Any<PartLocation>())
            .Returns(5);
        OpenDialog();

        var punch = Option(PhysicalAttackType.Punch);
        punch.LimbOptions.Single(option => option.Label == "Left Arm").Damage.ShouldBe(5);
        punch.LimbOptions.Single(option => option.Label == "Right Arm").Damage.ShouldBe(5);

        // A two-arm punch resolves as two attacks, so the combined option shows the sum.
        punch.LimbOptions.Single(option => option.Label == "Both Arms").Damage.ShouldBe(10);

        // Push does no damage, so it shows no damage indicator.
        Option(PhysicalAttackType.Push).LimbOptions
            .ShouldAllBe(option => !option.HasDamage);
    }

    [Fact]
    public void Declaration_OffersOneConfirmationAction()
    {
        OpenDialog();

        _sut.GetAvailableActions().Select(action => action.Label).ShouldBe(["Skip Attack"]);

        SelectRow(PhysicalAttackType.Punch);
        var action = _sut.GetAvailableActions().Single();
        action.Label.ShouldBe("Declare Attack");

        action.OnExecute();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration != null &&
            command.Declaration.AttackType == PhysicalAttackType.Punch));
    }

    [Fact]
    public void DeclaringWithOneArm_PublishesThatLimb()
    {
        OpenDialog();

        ChooseVariant(PhysicalAttackType.Punch, "Left Arm");
        _sut.ExecutePlayerAction();

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
        OpenDialog();

        SelectRow(PhysicalAttackType.Punch);
        _sut.ExecutePlayerAction();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration != null &&
            command.Declaration.AttackerLimbs.Count == 2));
    }

    [Fact]
    public void DeclaringAKick_PublishesOneLeg()
    {
        OpenDialog();

        SelectRow(PhysicalAttackType.Kick);
        _sut.ExecutePlayerAction();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration != null &&
            command.Declaration.AttackType == PhysicalAttackType.Kick &&
            command.Declaration.AttackerLimbs.Count == 1 &&
            command.Declaration.AttackerLimbs[0] == PartLocation.LeftLeg));
    }

    [Fact]
    public void DeclaringAPush_PublishesBothArms()
    {
        OpenDialog();

        SelectRow(PhysicalAttackType.Push);
        _sut.ExecutePlayerAction();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration != null &&
            command.Declaration.AttackType == PhysicalAttackType.Push &&
            command.Declaration.AttackerLimbs.Contains(PartLocation.LeftArm) &&
            command.Declaration.AttackerLimbs.Contains(PartLocation.RightArm) &&
            command.Declaration.AttackerLimbs.Count == 2 &&
            command.Declaration.TargetId == Enemy.Id));
    }

    [Fact]
    public void SelectingADisabledRow_DoesNothing()
    {
        OpenDialog(AttackerHex.GetNeighbour(HexDirection.BottomRight));

        var kick = Option(PhysicalAttackType.Kick);

        kick.IsEnabled.ShouldBeFalse();
        kick.LimbOptions.ShouldNotBeEmpty();
        kick.LimbOptions.ShouldAllBe(option => !option.IsEnabled);
        kick.IsSelected.ShouldBeFalse();
        _sut.SelectedAttackType.ShouldBeNull();
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.Declaration);
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<PhysicalAttackCommand>());
    }

    [Fact]
    public void PlayerAction_WithASelectedRow_Declares()
    {
        OpenDialog();

        _sut.PlayerActionLabel.ShouldBe("Skip Attack");
        SelectRow(PhysicalAttackType.Punch);

        _sut.PlayerActionLabel.ShouldBe("Declare Attack");
        _sut.ExecutePlayerAction();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration != null && command.Declaration.AttackerLimbs.Count == 2));
    }

    [Fact]
    public void PlayerAction_WithoutASelectedRow_Skips()
    {
        OpenDialog();

        _sut.ExecutePlayerAction();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration == null && command.UnitId == Attacker.Id));
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.SelectingUnit);
    }

    [Fact]
    public void PlayerAction_InTargetSelection_Skips()
    {
        DeployPair();
        SelectAttacker();

        _sut.PlayerActionLabel.ShouldBe("Skip Attack");
        _sut.ExecutePlayerAction();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration == null && command.UnitId == Attacker.Id));
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
    public void CloseDeclaration_ReturnsToTargetSelectionAndKeepsTheAttacker()
    {
        OpenDialog();
        SelectRow(PhysicalAttackType.Punch);

        _sut.CloseDeclaration();

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
        _sut.Attacker.ShouldBe(Attacker);
        _sut.SelectedTarget.ShouldBeNull();
        _sut.SelectedAttackType.ShouldBeNull();
        _sut.Options.ShouldBeEmpty();
        _viewModel.IsPhysicalAttackDeclarationVisible.ShouldBeFalse();
        _game.BattleMap!.GetHex(Enemy.Position!.Coordinates)!
            .Highlights.OfType<AttackReachableHighlight>().ShouldNotBeEmpty();
    }

    [Fact]
    public void ClickingAnInvalidHex_InDeclaration_ClosesTheDialog()
    {
        OpenDialog();

        _sut.HandleHexSelection(new Hex(new HexCoordinates(1, 1)));

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
        _sut.Attacker.ShouldBe(Attacker);
        _sut.SelectedTarget.ShouldBeNull();
        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<PhysicalAttackCommand>());
    }

    [Fact]
    public void ClickingAnInvalidHex_InTargetSelection_KeepsTheAttacker()
    {
        DeployPair();
        SelectAttacker();

        _sut.HandleHexSelection(new Hex(new HexCoordinates(1, 1)));

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
        _sut.Attacker.ShouldBe(Attacker);
        _sut.SelectedTarget.ShouldBeNull();
    }

    [Fact]
    public void ClickingAnotherReachableEnemy_RebuildsTheRows()
    {
        var firstTargetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var secondTargetHex = AttackerHex.GetNeighbour(HexDirection.TopRight);
        DeployPair(firstTargetHex);
        SecondEnemy.Deploy(new HexPosition(secondTargetHex, HexDirection.Bottom), null);
        StubCalculator();
        SelectAttacker();
        SelectTarget(Enemy);
        SelectRow(PhysicalAttackType.Punch);

        SelectTarget(SecondEnemy);

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.Declaration);
        _sut.SelectedTarget.ShouldBe(SecondEnemy);
        _sut.SelectedAttackType.ShouldBeNull();
        _sut.Options.ShouldNotBeEmpty();
    }

    [Fact]
    public void EligibilityChangeAfterTheDialogOpened_DoesNotPublishACommand()
    {
        OpenDialog();
        ChooseVariant(PhysicalAttackType.Punch, "Right Arm");

        // The selected limb is no longer eligible once its weapon has fired.
        FireWeaponIn(Attacker, PartLocation.RightArm);
        _sut.ExecutePlayerAction();

        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<PhysicalAttackCommand>());
    }

    [Fact]
    public void ImpossibleTargetNumber_IsStillDeclarable()
    {
        OpenDialog(targetNumber: 13);

        var punch = Option(PhysicalAttackType.Punch);
        punch.IsEnabled.ShouldBeTrue();
        punch.IsImpossible.ShouldBeTrue();
        punch.HitProbabilityText.ShouldBe("-");

        SelectRow(PhysicalAttackType.Punch);
        _sut.ExecutePlayerAction();

        _commandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(command =>
            command.Declaration != null &&
            command.Declaration.AttackType == PhysicalAttackType.Punch));
    }

    [Fact]
    public void LostPermission_BlocksDeclarations()
    {
        OpenDialog();
        SelectRow(PhysicalAttackType.Punch);

        SwitchActivePlayerToEnemy();

        _sut.ExecutePlayerAction();

        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<PhysicalAttackCommand>());
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
    public void SwitchingAttacker_InTargetSelection_ResetsTheSelection()
    {
        DeployPair();
        var secondEnemyHex = new HexCoordinates(3, 3);
        SecondEnemy.Deploy(new HexPosition(secondEnemyHex, HexDirection.Bottom), null);
        SecondAttacker.Deploy(
            new HexPosition(secondEnemyHex.GetNeighbour(HexDirection.Top), HexDirection.Bottom), null);
        SelectAttacker();

        _sut.HandleUnitSelectionFromList(SecondAttacker);

        _sut.Attacker.ShouldBe(SecondAttacker);
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
        _sut.SelectedTarget.ShouldBeNull();
        _viewModel.IsPhysicalAttackDeclarationVisible.ShouldBeFalse();
    }

    [Fact]
    public void SelectingAnAttacker_ExposesTheSelectedTypeOnlyAfterARowIsPicked()
    {
        OpenDialog();

        _sut.SelectedAttackType.ShouldBeNull();
        SelectRow(PhysicalAttackType.Kick);

        _sut.SelectedAttackType.ShouldBe(PhysicalAttackType.Kick);
    }

    [Fact]
    public void SelectingAnotherRow_DeselectsThePreviousRowAndAllItsLimbOptions()
    {
        OpenDialog();

        // 1. Select the right leg in Kick: that selects Kick.
        ChooseVariant(PhysicalAttackType.Kick, "Right Leg");
        var kick = Option(PhysicalAttackType.Kick);
        kick.IsSelected.ShouldBeTrue();
        _sut.SelectedAttackType.ShouldBe(PhysicalAttackType.Kick);

        // 2. Select both arms in Punch: that selects Punch and clears Kick and its subitems.
        ChooseVariant(PhysicalAttackType.Punch, "Both Arms");
        var punch = Option(PhysicalAttackType.Punch);

        kick.IsSelected.ShouldBeFalse();
        kick.LimbOptions.ShouldAllBe(limbOption => !limbOption.IsSelected);
        punch.IsSelected.ShouldBeTrue();
        punch.LimbOptions.Single(limbOption => limbOption.Label == "Both Arms")
            .IsSelected.ShouldBeTrue();
        _sut.SelectedAttackType.ShouldBe(PhysicalAttackType.Punch);
    }

    [Fact]
    public void TargetSelection_PromptsForTargetAndSelectsTheTargetUnit()
    {
        DeployPair();
        StubCalculator();
        SelectAttacker();

        _sut.ActionLabel.ShouldBe("Select Target");
        _sut.SelectedUnit.ShouldBe(Attacker);
        _sut.PlayerActionLabel.ShouldBe("Skip Attack");
        _sut.GetAvailableActions().Select(action => action.Label).ShouldBe(["Skip Attack"]);

        SelectTarget(Enemy);

        _sut.SelectedUnit.ShouldBe(Enemy);
    }

    [Fact]
    public void CanExecutePlayerAction_RequiresTargetOrDeclarationAndAVisibleActionMenu()
    {
        DeployPair();
        _sut.CanExecutePlayerAction.ShouldBeFalse();

        SelectAttacker();
        _sut.CanExecutePlayerAction.ShouldBeTrue();

        SelectTarget(Enemy);
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
    public void HandleUnitSelectionFromList_SelectsAnAttacker()
    {
        DeployPair();

        _sut.HandleUnitSelectionFromList(Attacker);

        _sut.Attacker.ShouldBe(Attacker);
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
    }

    [Fact]
    public void HandleUnitSelectionFromList_WithAnUnreachableEnemy_InTargetSelection_IsIgnored()
    {
        DeployPair();
        SecondEnemy.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top), null);
        SelectAttacker();

        _sut.HandleUnitSelectionFromList(SecondEnemy);

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
        _sut.SelectedTarget.ShouldBeNull();
        _sut.Attacker.ShouldBe(Attacker);
    }

    [Fact]
    public void UnitSelectionAfterLosingPermission_IsIgnored()
    {
        OpenDialog();
        SwitchActivePlayerToEnemy();

        _sut.HandleUnitSelectionFromList(Attacker);

        _sut.SelectedTarget.ShouldBe(Enemy);
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.Declaration);
    }

    [Fact]
    public void HandleFacingSelection_DoesNotChangeTheState()
    {
        DeployPair();
        SelectAttacker();

        _sut.HandleFacingSelection(HexDirection.Top);

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
        _sut.Attacker.ShouldBe(Attacker);
    }

    [Fact]
    public void CanSelectUnit_AllowsOwnUndeclaredUnitsBeforeDeclaration()
    {
        DeployPair();

        _sut.CanSelectUnit(null).ShouldBeTrue();
        _sut.CanSelectUnit(Attacker).ShouldBeTrue();
        _sut.CanSelectUnit(Enemy).ShouldBeFalse();

        SelectAttacker();
        _sut.CanSelectUnit(Attacker).ShouldBeTrue();

        SelectTarget(Enemy);
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
    public void Dialog_SideArcTarget_DisablesPushWithNoTargetInFront()
    {
        OpenDialog(AttackerHex.GetNeighbour(HexDirection.BottomRight));

        var push = Option(PhysicalAttackType.Push);

        push.IsEnabled.ShouldBeFalse();
        push.RestrictionReason.ShouldBe("No unit directly in front");
    }

    [Fact]
    public void Dialog_TorsoTwist_DoesNotCreateAPushTarget()
    {
        // Arrange: the target is in the feet's side arc but the torso faces it
        OpenDialog(AttackerHex.GetNeighbour(HexDirection.BottomRight));
        ((Mech)Attacker).RotateTorso(HexDirection.TopRight);

        // Act
        _sut.CloseDeclaration();
        SelectTarget(Enemy);
        var push = Option(PhysicalAttackType.Push);

        // Assert
        push.IsEnabled.ShouldBeFalse();
        push.RestrictionReason.ShouldBe("No unit directly in front");
    }

    [Fact]
    public void Dialog_ProneTarget_DisablesPushBecauseTheTargetMustStand()
    {
        DeployPair();
        ((Mech)Enemy).SetProne();
        StubCalculator();
        SelectAttacker();
        SelectTarget(Enemy);

        var push = Option(PhysicalAttackType.Push);

        push.IsEnabled.ShouldBeFalse();
        push.RestrictionReason.ShouldBe("The target must be a standing Mech");
    }

    [Fact]
    public void Dialog_ElevationMismatch_DisablesPush()
    {
        DeployPair();
        _game.BattleMap!.AddHex(new Hex(Enemy.Position!.Coordinates, 1));
        StubCalculator();
        SelectAttacker();
        SelectTarget(Enemy);

        var push = Option(PhysicalAttackType.Push);

        push.IsEnabled.ShouldBeFalse();
        push.RestrictionReason.ShouldBe("The target must be at the same elevation");
    }

    [Fact]
    public void Dialog_FiredArmWeapon_DisablesPush()
    {
        DeployPair();
        FireWeaponIn(Attacker, PartLocation.RightArm);
        StubCalculator();
        SelectAttacker();
        SelectTarget(Enemy);

        var push = Option(PhysicalAttackType.Push);

        push.IsEnabled.ShouldBeFalse();
        push.RestrictionReason.ShouldBe("An arm-mounted weapon was fired this turn");
    }

    [Fact]
    public void Dialog_TargetAlreadyReservedForADisplacement_DisablesPush()
    {
        DeployPair();
        var otherAttacker = _viewModel.Units.Last(unit =>
            unit.Owner!.Id == _player.Id && !ReferenceEquals(unit, Attacker));
        otherAttacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top), null);
        otherAttacker.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Push,
            AttackerLimbs = [PartLocation.LeftArm, PartLocation.RightArm],
            TargetId = Enemy.Id
        });
        StubCalculator();
        SelectAttacker();
        SelectTarget(Enemy);

        var push = Option(PhysicalAttackType.Push);

        push.IsEnabled.ShouldBeFalse();
        push.RestrictionReason.ShouldBe("The target is already being displaced this turn");
    }

    [Fact]
    public void Dialog_UndeployedAttacker_DisablesPushWithNoTargetInFront()
    {
        // Arrange
        DeployPair();
        StubCalculator();
        SelectAttacker();
        Attacker.RemoveFromBoard();

        // Act
        SelectTarget(Enemy);
        var push = Option(PhysicalAttackType.Push);

        // Assert
        push.IsEnabled.ShouldBeFalse();
        push.RestrictionReason.ShouldBe("No unit directly in front");
    }

    [Fact]
    public void DeclarationStep_PromptsToSelectAnAttack()
    {
        OpenDialog();

        _sut.ActionLabel.ShouldBe("Select attack");
    }

    [Fact]
    public void SelectingAFriendlyUnitInDeclaration_ClosesTheDialog()
    {
        OpenDialog();
        SelectRow(PhysicalAttackType.Punch);

        _sut.HandleUnitSelectionFromList(Attacker);

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
        _sut.SelectedTarget.ShouldBeNull();
        _sut.SelectedAttackType.ShouldBeNull();
        _viewModel.IsPhysicalAttackDeclarationVisible.ShouldBeFalse();
    }

    [Fact]
    public void UncheckingTheSelectedLimb_ClearsTheSelectedAttackType()
    {
        OpenDialog();
        ChooseVariant(PhysicalAttackType.Punch, "Both Arms");
        var bothArms = Option(PhysicalAttackType.Punch).LimbOptions
            .Single(limbOption => limbOption.Label == "Both Arms");
        _sut.SelectedAttackType.ShouldBe(PhysicalAttackType.Punch);

        bothArms.IsSelected = false;

        _sut.SelectedAttackType.ShouldBeNull();
        Option(PhysicalAttackType.Punch).IsSelected.ShouldBeFalse();
    }

    [Fact]
    public void CloseDeclaration_WithoutAReachableTarget_ClearsTheHighlights()
    {
        OpenDialog();
        var enemyHex = Enemy.Position!.Coordinates;
        _game.BattleMap!.GetHex(enemyHex)!
            .Highlights.OfType<AttackReachableHighlight>().ShouldNotBeEmpty();

        Destroy(Enemy);
        _sut.CloseDeclaration();

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
        _game.BattleMap!.GetHex(enemyHex)!
            .Highlights.OfType<AttackReachableHighlight>().ShouldBeEmpty();
    }

    [Fact]
    public void TargetReservedAfterTheDialogOpened_DoesNotPublishACommand()
    {
        OpenDialog();
        ChooseVariant(PhysicalAttackType.Push, "Both Arms");
        var otherAttacker = _viewModel.Units.Last(unit =>
            unit.Owner!.Id == _player.Id && !ReferenceEquals(unit, Attacker));
        otherAttacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Top), null);
        otherAttacker.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Push,
            AttackerLimbs = [PartLocation.LeftArm, PartLocation.RightArm],
            TargetId = Enemy.Id
        });

        _sut.ExecutePlayerAction();

        _commandPublisher.DidNotReceive().PublishCommand(Arg.Any<PhysicalAttackCommand>());
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.Declaration);
        _sut.SelectedAttackType.ShouldBe(PhysicalAttackType.Push);
    }

    [Fact]
    public void DestroyedUnitFromTheList_IsNotSelected()
    {
        DeployPair();
        Destroy(Enemy);

        _sut.HandleUnitSelectionFromList(Enemy);

        _sut.Attacker.ShouldBeNull();
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.SelectingUnit);
    }

    [Fact]
    public void SwitchingToAFriendlyUnitThatCannotAttack_InTargetSelection_KeepsTheCurrentAttacker()
    {
        DeployPair();
        SelectAttacker();
        Attacker.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Punch,
            AttackerLimbs = [PartLocation.LeftArm],
            TargetId = Enemy.Id
        });

        _sut.HandleUnitSelectionFromList(Attacker);

        _sut.Attacker.ShouldBe(Attacker);
        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.TargetSelection);
    }

    [Fact]
    public void CloseDeclaration_BeforeDeclaration_DoesNothing()
    {
        DeployPair();

        _sut.CloseDeclaration();

        _sut.CurrentStep.ShouldBe(PhysicalAttackStep.SelectingUnit);
        _sut.Attacker.ShouldBeNull();
    }

    private static void FireWeaponIn(IUnit unit, PartLocation location)
    {
        var laser = new MediumLaser();
        unit.Parts[location].TryAddComponent(laser).ShouldBeTrue();
        laser.MarkAsFired();
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
