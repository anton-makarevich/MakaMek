using System.Globalization;
using Avalonia.Media;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;
using MakaMek.Avalonia.Tests.TestHelpers;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Services;

namespace MakaMek.Avalonia.Tests.Converters;

public class ComponentStatusBackgroundConverterTests
{
    private readonly IAvaloniaResourcesLocator _resourcesLocator;
    private readonly ComponentStatusBackgroundConverter _sut;

    public ComponentStatusBackgroundConverterTests()
    {
        _resourcesLocator = Substitute.For<IAvaloniaResourcesLocator>();
        ComponentStatusBackgroundConverter.Initialize(_resourcesLocator);
        _sut = new ComponentStatusBackgroundConverter();
    }

    [Fact]
    public void Convert_AvailableWeapon_ReturnsTransparent()
    {
        // Arrange
        var weapon = new TestWeapon();
        var unitPart = new Arm("Left Arm",PartLocation.LeftArm,1,1);
        weapon.Mount([1], unitPart);
        
        // Act
        var result = _sut.Convert(weapon, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Transparent);
    }
        
    [Fact]
    public void Convert_WeaponOnDestroyedPart_ReturnsGray()
    {
        // Arrange
        var weapon = new TestWeapon();
        var unitPart = new Arm("Left Arm",PartLocation.LeftArm,1,1);
        unitPart.ApplyDamage(5);
        weapon.Mount([1], unitPart);
        
        // Act
        var result = _sut.Convert(weapon, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Gray);
    }

    [Fact]  // Not mounted
    public void Convert_UnavailableWeapon_ReturnsGray()
    {
        // Arrange
        var weapon = new TestWeapon();
        weapon.Deactivate();

        // Act
        var result = _sut.Convert(weapon, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Gray);
    }

    [Fact]
    public void Convert_DestroyedWeapon_ReturnsRed()
    {
        // Arrange
        var weapon = new TestWeapon();
        weapon.Hit();
        var destroyedBrush = new SolidColorBrush(Colors.DarkRed);
        _resourcesLocator.TryFindResource("DestroyedBrush").Returns(destroyedBrush);

        // Act
        var result = _sut.Convert(weapon, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(destroyedBrush);
    }

    [Fact]
    public void Convert_DestroyedWeapon_ReturnsDefaultWhenResourceNotFound()
    {
        // Arrange
        var weapon = new TestWeapon();
        weapon.Hit();
        var sut = new ComponentStatusBackgroundConverter();

        // Act
        var result = sut.Convert(weapon, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Red);
    }

    [Fact]
    public void Convert_NullWeapon_ReturnsTransparent()
    {
        // Act
        var result = _sut.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Transparent);
    }

    [Fact]
    public void Convert_NonWeaponObject_ReturnsTransparent()
    {
        // Act
        var result = _sut.Convert("not a weapon", typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Transparent);
    }
    
    [Fact]
    public void Convert_Status_ToColor()
    {
        // Arrange
        var destroyedBrush = new SolidColorBrush(Colors.DarkRed);
        _resourcesLocator.TryFindResource("DestroyedBrush").Returns(destroyedBrush);

        // Act
        var result = _sut.Convert(ComponentStatus.Destroyed, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(destroyedBrush);
    }

    [Fact]
    public void Convert_Status_ToDefaultColorWhenResourceNotFound()
    {
        // Arrange
        _resourcesLocator.TryFindResource("DestroyedBrush").Returns(null);

        // Act
        var result = _sut.Convert(ComponentStatus.Destroyed, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Red);
    }

    [Fact]
    public void Convert_DamagedStatus_ReturnsDamagedBrush()
    {
        // Arrange
        var damagedBrush = new SolidColorBrush(Colors.DarkOrange);
        _resourcesLocator.TryFindResource("DamagedBrush").Returns(damagedBrush);

        // Act
        var result = _sut.Convert(ComponentStatus.Damaged, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(damagedBrush);
    }

    [Fact]
    public void Convert_DamagedStatus_ReturnsDefaultWhenResourceNotFound()
    {
        // Arrange
        _resourcesLocator.TryFindResource("DamagedBrush").Returns(null);

        // Act
        var result = _sut.Convert(ComponentStatus.Damaged, typeof(IBrush), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Orange);
    }

    [Fact]
    public void Convert_InvalidTargetType_ReturnsTransparent()
    {
        // Arrange
        var weapon = new TestWeapon();

        // Act
        var result = _sut.Convert(weapon, typeof(string), null, CultureInfo.InvariantCulture) as SolidColorBrush;

        // Assert
        result.ShouldNotBeNull();
        result.Color.ShouldBe(Colors.Transparent);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() =>
            _sut.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }
}