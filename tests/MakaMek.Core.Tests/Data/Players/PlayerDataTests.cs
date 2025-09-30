using Sanet.MakaMek.Core.Data.Game.Players;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Players;

public class PlayerDataTests
{
    [Fact]
    public void CreateDefault_ShouldInitializeProperties()
    {
        // Act
        var sut = PlayerData.CreateDefault();
        
        // Assert
        sut.Id.ShouldNotBe(Guid.Empty);
        sut.Name.ShouldStartWith("Player");
        sut.Name.Length.ShouldBe(11);
        int.TryParse(sut.Name[7..], out _).ShouldBeTrue();
        sut.Tint.ShouldBe("#FFFFFF");
    }
}