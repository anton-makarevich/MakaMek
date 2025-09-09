using Sanet.MakaMek.Core.Data.Units;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components;

public class JumpJetsFacts
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var sut = new JumpJets();

        // Assert
        sut.Name.ShouldBe("Jump Jets");
        sut.Size.ShouldBe(1);
        sut.JumpMp.ShouldBe(1);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.JumpJet);
        sut.IsRemovable.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithCustomJumpMp_SetsCorrectValues()
    {
        // Arrange & Act
        var sut = new JumpJets(2);

        // Assert
        sut.Name.ShouldBe("Jump Jets");
        sut.Size.ShouldBe(1);
        sut.JumpMp.ShouldBe(2);
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_SetsIsDestroyedToTrue()
    {
        // Arrange
        var sut = new JumpJets();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
}
