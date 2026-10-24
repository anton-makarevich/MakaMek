using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class AttackerProneGateTests
{
    private readonly AttackerProneGate _sut = new();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldSkip_OnlyWhenTheAttackerIsProne(bool isProne)
    {
        // Arrange
        var attacker = new MechFactory(
                new TotalWarfareRulesProvider(),
                new ClassicBattletechComponentProvider(),
                Substitute.For<ILocalizationService>())
            .Create(MechFactoryTests.CreateDummyMechData());
        if (isProne) attacker.SetProne();

        // Act
        var result = _sut.ShouldSkip(CreateContext(attacker), _logger);

        // Assert
        result.ShouldBe(isProne);
        _logger.ReceivedCalls().Count().ShouldBe(isProne ? 1 : 0);
    }

    [Fact]
    public void ShouldSkip_IsFalseForANonMechAttacker()
    {
        // Act + Assert
        _sut.ShouldSkip(CreateContext(Substitute.For<IUnit>()), _logger).ShouldBeFalse();
    }

    private static PhysicalAttackContext CreateContext(IUnit attacker) => new()
    {
        Attacker = attacker,
        Target = Substitute.For<IUnit>(),
        AttackType = PhysicalAttackType.Kick,
        AttackerLimb = PartLocation.LeftLeg,
        Map = Substitute.For<IBattleMap>()
    };
}
