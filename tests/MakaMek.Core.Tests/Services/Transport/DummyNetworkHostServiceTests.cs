using Sanet.MakaMek.Core.Services.Transport;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Transport;

public class DummyNetworkHostServiceTests
{
    private readonly DummyNetworkHostService _service = new();

    [Fact]
    public void Publisher_ShouldAlwaysBeNull()
    {
        _service.Publisher.ShouldBeNull();
    }

    [Fact]
    public void HubUrl_ShouldAlwaysBeNull()
    {
        _service.HubUrl.ShouldBeNull();
    }

    [Fact]
    public void IsRunning_ShouldAlwaysBeFalse()
    {
        _service.IsRunning.ShouldBeFalse();
    }
    
    [Fact]
    public void CanStart_ShouldAlwaysBeFalse()
    {
        _service.CanStart.ShouldBeFalse();
    }

    [Fact]
    public async Task Start_ShouldCompleteSuccessfully()
    {
        // Act
        var task = _service.Start();

        // Assert
        await Should.NotThrowAsync(task); 
        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Stop_ShouldCompleteSuccessfully()
    {
        // Act
        var task = _service.Stop();

        // Assert
        await Should.NotThrowAsync(task);
        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        Should.NotThrow(() => _service.Dispose());
    }
}
