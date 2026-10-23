using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class AttackerDestroyedGateTests
{
    private readonly AttackerDestroyedGate _sut = new();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldSkip_OnlyWhenTheAttackerIsDestroyed(bool isDestroyed)
    {
        // Arrange
        var attacker = Substitute.For<IUnit>();
        attacker.IsDestroyed.Returns(isDestroyed);

        // Act
        var result = _sut.ShouldSkip(CreateContext(attacker), _logger);

        // Assert
        result.ShouldBe(isDestroyed);
        _logger.ReceivedCalls().Count().ShouldBe(isDestroyed ? 1 : 0);
    }

    private static PhysicalAttackContext CreateContext(IUnit attacker) => new()
    {
        Attacker = attacker,
        Target = Substitute.For<IUnit>(),
        AttackType = PhysicalAttackType.Punch,
        AttackerLimb = PartLocation.LeftArm,
        Map = Substitute.For<IBattleMap>()
    };
}
