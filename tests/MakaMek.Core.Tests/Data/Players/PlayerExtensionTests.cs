using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Players;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Players;

public class PlayerExtensionTests
{
    [Fact]
    public void ToData_ShouldConvertPlayerToPlayerData()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Test Player", PlayerControlType.Human);
        
        // Act
        var data = player.ToData();
        
        // Assert
        data.Id.ShouldBe(player.Id);
        data.Name.ShouldBe(player.Name);
        data.Tint.ShouldBe(player.Tint);
    }
}