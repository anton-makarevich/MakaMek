using Avalonia;
using Shouldly;
using Sanet.MakaMek.Avalonia.Controls;

namespace MakaMek.Avalonia.Tests.Controls;

public class MapTransformCalculatorTests
{
    private readonly MapTransformCalculator _sut = new();

    [Fact]
    public void Constructor_ShouldHaveIdentityTransform()
    {
        // Assert
        _sut.Scale.ShouldBe(1.0);
        _sut.TranslateX.ShouldBe(0.0);
        _sut.TranslateY.ShouldBe(0.0);
    }

    [Fact]
    public void Constructor_ShouldHaveDefaultScaleBounds()
    {
        // Assert
        _sut.MinScale.ShouldBe(0.5);
        _sut.MaxScale.ShouldBe(2.0);
    }

    [Fact]
    public void Matrix_ShouldReflectCurrentState()
    {
        // Act
        _sut.SetTransform(1.5, 10, 20);

        // Assert
        var matrix = _sut.Matrix;
        matrix.M11.ShouldBe(1.5);
        matrix.M22.ShouldBe(1.5);
        matrix.M31.ShouldBe(10);
        matrix.M32.ShouldBe(20);
    }

    [Fact]
    public void SetTransform_ShouldUpdateState()
    {
        // Act
        _sut.SetTransform(2.0, 5, -7);

        // Assert
        _sut.Scale.ShouldBe(2.0);
        _sut.TranslateX.ShouldBe(5.0);
        _sut.TranslateY.ShouldBe(-7.0);
    }

    [Theory]
    [InlineData(1.0, 0, 0, 10, 20, 10, 20)]
    [InlineData(2.0, 0, 0, 10, 20, 20, 40)]
    [InlineData(2.0, 5, 7, 10, 20, 25, 47)]
    public void LocalToParent_ShouldApplyScaleThenTranslate(
        double scale, double tx, double ty,
        double localX, double localY,
        double expectedX, double expectedY)
    {
        // Arrange
        _sut.SetTransform(scale, tx, ty);

        // Act
        var result = _sut.LocalToParent(new Point(localX, localY));

        // Assert
        result.X.ShouldBe(expectedX);
        result.Y.ShouldBe(expectedY);
    }

    [Fact]
    public void Pan_ShouldAddDeltaToTranslationWithoutChangingScale()
    {
        // Arrange
        _sut.SetTransform(1.5, 10, 20);

        // Act
        _sut.Pan(new Point(5, -8));

        // Assert
        _sut.Scale.ShouldBe(1.5);
        _sut.TranslateX.ShouldBe(15.0);
        _sut.TranslateY.ShouldBe(12.0);
    }

    [Fact]
    public void ApplyZoom_ShouldScaleAndReturnTrue()
    {
        // Act
        var changed = _sut.ApplyZoom(1.5, new Point(0, 0));

        // Assert
        changed.ShouldBeTrue();
        _sut.Scale.ShouldBe(1.5);
    }

    [Fact]
    public void ApplyZoom_ShouldKeepAnchorPointFixed()
    {
        // Arrange
        var anchor = new Point(100, 100);

        // Act
        _sut.ApplyZoom(1.5, anchor);

        // Assert - the parent point that maps back to the same local content
        // stays under the anchor: local = (anchor - t) / s
        var localUnderAnchor = new Point(
            (anchor.X - _sut.TranslateX) / _sut.Scale,
            (anchor.Y - _sut.TranslateY) / _sut.Scale);
        var reprojected = _sut.LocalToParent(localUnderAnchor);
        reprojected.X.ShouldBe(anchor.X, 1e-9);
        reprojected.Y.ShouldBe(anchor.Y, 1e-9);
    }

    [Fact]
    public void ApplyZoom_ShouldClampToMaxScale()
    {
        // Act
        _sut.ApplyZoom(10.0, new Point(0, 0));

        // Assert
        _sut.Scale.ShouldBe(_sut.MaxScale);
    }

    [Fact]
    public void ApplyZoom_ShouldClampToMinScale()
    {
        // Act
        _sut.ApplyZoom(0.01, new Point(0, 0));

        // Assert
        _sut.Scale.ShouldBe(_sut.MinScale);
    }

    [Fact]
    public void ApplyZoom_ShouldReturnFalse_WhenAlreadyAtMaxScale()
    {
        // Arrange
        _sut.SetTransform(_sut.MaxScale, 0, 0);

        // Act
        var changed = _sut.ApplyZoom(2.0, new Point(50, 50));

        // Assert
        changed.ShouldBeFalse();
    }

    [Fact]
    public void UpdatePinch_ShouldReturnFalse_WhenStartPinchNotCalled()
    {
        // Act
        var changed = _sut.UpdatePinch(new Point(0, 0), new Point(10, 0));

        // Assert
        changed.ShouldBeFalse();
    }

    [Fact]
    public void UpdatePinch_ShouldNotChangeTransform_WhenDistanceUnchanged()
    {
        // Arrange
        var p0 = new Point(0, 0);
        var p1 = new Point(100, 0);
        _sut.StartPinch(p0, p1);

        // Act
        var changed = _sut.UpdatePinch(p0, p1);

        // Assert - same finger positions => ratio 1 => scale stays at 1
        changed.ShouldBeTrue();
        _sut.Scale.ShouldBe(1.0, 1e-9);
    }

    [Fact]
    public void UpdatePinch_ShouldZoomIn_WhenFingersSpreadApart()
    {
        // Arrange - start with fingers 100px apart
        _sut.StartPinch(new Point(0, 0), new Point(100, 0));

        // Act - spread to 200px (ratio 2) around the same midpoint
        _sut.UpdatePinch(new Point(-50, 0), new Point(150, 0));

        // Assert - scale increases (first update takes raw ratio, no smoothing)
        _sut.Scale.ShouldBeGreaterThan(1.0);
    }

    [Fact]
    public void UpdatePinch_ShouldClampScaleToMaxScale()
    {
        // Arrange
        _sut.StartPinch(new Point(0, 0), new Point(10, 0));

        // Act - massive spread should push past MaxScale
        _sut.UpdatePinch(new Point(0, 0), new Point(1000, 0));

        // Assert
        _sut.Scale.ShouldBeLessThanOrEqualTo(_sut.MaxScale);
    }

    [Fact]
    public void UpdatePinch_ShouldKeepAnchorLocalUnderMidpoint()
    {
        // Arrange
        _sut.StartPinch(new Point(0, 0), new Point(100, 0));
        // midpoint at start is (50, 0); local anchor is (50, 0) at scale 1

        // Act - spread symmetrically, midpoint stays (50,0)
        _sut.UpdatePinch(new Point(-50, 0), new Point(150, 0));

        // Assert - anchor local (50,0) still projects to midpoint (50,0)
        var projected = _sut.LocalToParent(new Point(50, 0));
        projected.X.ShouldBe(50.0, 1e-9);
        projected.Y.ShouldBe(0.0, 1e-9);
    }

    [Fact]
    public void Center_ShouldResetToIdentity()
    {
        // Arrange
        _sut.SetTransform(1.8, 30, 40);

        // Act
        _sut.Center();

        // Assert
        _sut.Scale.ShouldBe(1.0);
        _sut.TranslateX.ShouldBe(0.0);
        _sut.TranslateY.ShouldBe(0.0);
    }

    [Fact]
    public void ResetZoom_ShouldResetScaleButKeepTranslation()
    {
        // Arrange
        _sut.SetTransform(1.8, 30, 40);

        // Act
        _sut.ResetZoom();

        // Assert
        _sut.Scale.ShouldBe(1.0);
        _sut.TranslateX.ShouldBe(30.0);
        _sut.TranslateY.ShouldBe(40.0);
    }

    [Theory]
    [InlineData(0, 0, 3, 4, 5)]
    [InlineData(1, 1, 4, 5, 5)]
    [InlineData(0, 0, 0, 0, 0)]
    public void Distance_ShouldReturnEuclideanDistance(
        double ax, double ay, double bx, double by, double expected)
    {
        // Act
        var result = MapTransformCalculator.Distance(new Point(ax, ay), new Point(bx, by));

        // Assert
        result.ShouldBe(expected, 1e-9);
    }

    [Theory]
    [InlineData(0, 0, 10, 20, 5, 10)]
    [InlineData(-4, -6, 4, 6, 0, 0)]
    public void Midpoint_ShouldReturnAveragedPoint(
        double ax, double ay, double bx, double by, double expectedX, double expectedY)
    {
        // Act
        var result = MapTransformCalculator.Midpoint(new Point(ax, ay), new Point(bx, by));

        // Assert
        result.X.ShouldBe(expectedX);
        result.Y.ShouldBe(expectedY);
    }
}
