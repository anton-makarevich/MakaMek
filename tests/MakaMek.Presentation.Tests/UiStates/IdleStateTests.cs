using Sanet.MakaMek.Presentation.UiStates;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class IdleStateTests
{
    private readonly IdleState _sut;

    public IdleStateTests()
    {
        _sut = new IdleState();
    }
    
    [Fact]
    public void ActionLabel_ShouldBeWait()
    {
        // Assert
        _sut.ActionLabel.ShouldBe("Wait");
    }
    
    [Fact]
    public void IsActionRequired_ShouldBeFalse()
    {
        // Assert
        _sut.IsActionRequired.ShouldBeFalse();
    }
}