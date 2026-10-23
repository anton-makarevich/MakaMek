using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class PhysicalAttackTargetValidityGateTests
{
    private readonly IPhysicalAttackEligibility _eligibility = Substitute.For<IPhysicalAttackEligibility>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly PhysicalAttackTargetValidityGate _sut;

    public PhysicalAttackTargetValidityGateTests()
    {
        _sut = new PhysicalAttackTargetValidityGate(_eligibility);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldSkip_WhenTheEligibilityServiceSaysTheLimbCanNoLongerBeUsed(bool canResolve)
    {
        // Arrange
        var context = new PhysicalAttackContext
        {
            Attacker = Substitute.For<IUnit>(),
            Target = Substitute.For<IUnit>(),
            AttackType = PhysicalAttackType.Punch,
            AttackerLimb = PartLocation.RightArm,
            Map = Substitute.For<IBattleMap>()
        };
        _eligibility.CanResolve(context.Attacker, context.Target, PhysicalAttackType.Punch, PartLocation.RightArm,
            context.Map).Returns(canResolve);

        // Act
        var result = _sut.ShouldSkip(context, _logger);

        // Assert
        result.ShouldBe(!canResolve);
        _logger.ReceivedCalls().Count().ShouldBe(canResolve ? 0 : 1);
    }
}
