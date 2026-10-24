using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Attack;

public class AttackerStateModifierResolverTests
{
    [Fact]
    public void Resolve_ProneAttacker_ReturnsProneModifierWithDefaultValue()
    {
        // Act
        var result = AttackerStateModifierResolver.Resolve(true, false);

        // Assert
        var modifier = result.ShouldNotBeNull().ShouldBeOfType<ProneAttackerModifier>();
        modifier.Value.ShouldBe(ProneAttackerModifier.DefaultValue);
    }

    [Fact]
    public void Resolve_SkiddingAttacker_ReturnsSkiddingModifierWithDefaultValue()
    {
        // Act
        var result = AttackerStateModifierResolver.Resolve(false, true);

        // Assert
        var modifier = result.ShouldNotBeNull().ShouldBeOfType<SkiddingAttackerModifier>();
        modifier.Value.ShouldBe(SkiddingAttackerModifier.DefaultValue);
    }

    [Fact]
    public void Resolve_ProneAndSkiddingAttacker_ReturnsOnlyProneModifier()
    {
        // Act — prone wins over skidding
        var result = AttackerStateModifierResolver.Resolve(true, true);

        // Assert
        var modifier = result.ShouldNotBeNull().ShouldBeOfType<ProneAttackerModifier>();
        modifier.Value.ShouldBe(ProneAttackerModifier.DefaultValue);
    }

    [Fact]
    public void Resolve_NeitherProneNorSkiddingAttacker_ReturnsNull()
    {
        // Act
        var result = AttackerStateModifierResolver.Resolve(false, false);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void IsAttackerStateModifier_IdentifiesProneAndSkiddingModifiers()
    {
        // Assert
        AttackerStateModifierResolver.IsAttackerStateModifier(
            new ProneAttackerModifier { Value = ProneAttackerModifier.DefaultValue }).ShouldBeTrue();
        AttackerStateModifierResolver.IsAttackerStateModifier(
            new SkiddingAttackerModifier { Value = SkiddingAttackerModifier.DefaultValue }).ShouldBeTrue();
        AttackerStateModifierResolver.IsAttackerStateModifier(
            new HeatRollModifier { Value = 2, HeatLevel = 15 }).ShouldBeFalse();
    }
}
