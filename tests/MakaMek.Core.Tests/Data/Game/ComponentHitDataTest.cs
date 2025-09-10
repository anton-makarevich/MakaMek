using JetBrains.Annotations;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game;

[TestSubject(typeof(ComponentHitData))]
public class ComponentHitDataTest
{
    private readonly IDamageTransferCalculator _damageTransferCalculator = Substitute.For<IDamageTransferCalculator>();
    private readonly Unit _unit;

    public ComponentHitDataTest()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        _unit = new MechFactory(new ClassicBattletechRulesProvider(), Substitute.For<ILocalizationService>())
            .Create(unitData);
        
        
        _damageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(p => p == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([new LocationDamageData(PartLocation.CenterTorso, 0, 5, false)]);
    }
    
    [Fact]
    public void CreateComponentHitData_ShouldCreateCorrectData_WhenComponentHasNoExplosionDamage()
    {
        // Arrange
        var part = _unit.Parts[PartLocation.CenterTorso];
        
        _damageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(p => p == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([]);
        
        // Act
        var result = ComponentHitData.CreateComponentHitData(part, 1, _damageTransferCalculator);
        
        // Assert
        result.Slot.ShouldBe(1);
        result.Type.ShouldBe(MakaMekComponent.Engine);
        result.ExplosionDamage.ShouldBe(0);
        result.ExplosionDamageDistribution.ShouldBeEmpty();
    }
    
    [Fact]
    public void CreateComponentHitData_ShouldCreateCorrectData_WhenComponentHasExplosionDamage()
    {
        // Arrange
        var part = _unit.Parts[PartLocation.CenterTorso];
        var ammo = new Ammo(Lrm5.Definition, 1);
        part.TryAddComponent(ammo, [10]).ShouldBeTrue();
        
        // Act
        var result = ComponentHitData.CreateComponentHitData(part, 10, _damageTransferCalculator);
        
        // Assert
        result.Slot.ShouldBe(10);
        result.Type.ShouldBe(MakaMekComponent.ISAmmoLRM5);
        result.ExplosionDamage.ShouldBe(5);
        result.ExplosionDamageDistribution.ShouldNotBeEmpty();
        result.ExplosionDamageDistribution.Length.ShouldBe(1);
        result.ExplosionDamageDistribution[0].Location.ShouldBe(PartLocation.CenterTorso);
        result.ExplosionDamageDistribution[0].StructureDamage.ShouldBe(5);
    }
    
    [Fact]
    public void CreateComponentHitData_ShouldThrow_WhenSlotIsInvalid()
    {
        // Arrange
        var part = _unit.Parts[PartLocation.CenterTorso];
        
        // Act & Assert
        Should.Throw<ArgumentException>(() => ComponentHitData.CreateComponentHitData(part, 99, _damageTransferCalculator))
            .Message.ShouldContain("Invalid slot");
    }
    
    [Fact]
    public void CreateComponentHitData_ShouldThrow_WhenPartHasNoUnit()
    {
        // Arrange
        var part = new CenterTorso("CenterTorso", 10, 5, 10);
        
        // Act & Assert
        Should.Throw<ArgumentException>(() => ComponentHitData.CreateComponentHitData(part, 1, _damageTransferCalculator))
            .Message.ShouldContain("Detached part");
    }
}