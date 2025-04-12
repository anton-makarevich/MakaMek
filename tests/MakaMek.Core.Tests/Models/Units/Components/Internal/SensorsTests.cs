﻿using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Internal;

public class SensorsTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Sensors();

        // Assert
        sut.Name.ShouldBe("Sensors");
        sut.MountedAtSlots.ToList().Count.ShouldBe(2);
        sut.MountedAtSlots.ShouldBe([1,4]);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.Sensors);
        sut.IsRemovable.ShouldBeFalse();
    }
}