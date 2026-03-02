using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class MovementPathCacheTests
{
    private readonly MovementPathCache _sut = new();

    [Fact]
    public void Add_And_Get_ShouldWork()
    {
        var start = new HexPosition(1, 1, HexDirection.Top);
        var dest = new HexPosition(2, 2, HexDirection.Bottom);
        // Create dummy segments
        var segments = new List<PathSegment>
        {
            new(start, dest, 1)
        };
        var path = new MovementPath(segments, MovementType.Walk);

        _sut.Add(path);

        var result = _sut.Get(start, dest, false);
        result.ShouldBe(path);
    }

    [Fact]
    public void Get_ShouldReturnNull_WhenNotFound()
    {
        var start = new HexPosition(1, 1, HexDirection.Top);
        var dest = new HexPosition(2, 2, HexDirection.Bottom);
        
        var result = _sut.Get(start, dest, false);
        result.ShouldBeNull();
    }

    [Fact]
    public void Invalidate_ShouldRemovePath_WhenCoordinateInPath()
    {
        var start = new HexPosition(1, 1, HexDirection.Top);
        var mid = new HexPosition(1, 2, HexDirection.Top);
        var dest = new HexPosition(1, 3, HexDirection.Top);
        
        var segments = new List<PathSegment>
        {
            new(start, mid, 1),
            new(mid, dest, 1)
        };
        var path = new MovementPath(segments, MovementType.Walk);
        
        _sut.Add(path);
        
        // Invalidate something not in the path
        _sut.Invalidate(new HexCoordinates(5, 5));
        _sut.Get(start, dest, false).ShouldBe(path);
        
        // Invalidate internal hex
        _sut.Invalidate(mid.Coordinates);
        _sut.Get(start, dest, false).ShouldBeNull();
    }
    
    [Fact]
    public void Invalidate_ShouldRemovePath_WhenStartOrDestInPath()
    {
        var start = new HexPosition(1, 1, HexDirection.Top);
        var dest = new HexPosition(1, 2, HexDirection.Top);
        
        var segments = new List<PathSegment>
        {
            new(start, dest, 1)
        };
        var path = new MovementPath(segments, MovementType.Walk);
        
        _sut.Add(path);
        
        // Invalidate start hex
        _sut.Invalidate(start.Coordinates);
        _sut.Get(start, dest, false).ShouldBeNull();
    }

    [Fact]
    public void Cache_ShouldDifferentiateByIsJump()
    {
        var start = new HexPosition(1, 1, HexDirection.Top);
        var dest = new HexPosition(2, 2, HexDirection.Bottom);
        
        var walkSegments = new List<PathSegment> { new(start, dest, 2) };
        var walkPath = new MovementPath(walkSegments, MovementType.Walk);
        
        var jumpSegments = new List<PathSegment> { new(start, dest, 1) };
        var jumpPath = new MovementPath(jumpSegments, MovementType.Jump);
        
        _sut.Add(walkPath);
        _sut.Add(jumpPath);
        
        _sut.Get(start, dest, false).ShouldBe(walkPath);
        _sut.Get(start, dest, true).ShouldBe(jumpPath);
    }
    
    [Fact]
    public void GetHashCode_And_Equals_ShouldWorkCorrectly()
    {
        var start = new HexPosition(1, 1, HexDirection.Top);
        var dest = new HexPosition(2, 2, HexDirection.Bottom);
        
        var path1 = new MovementPath([new PathSegment(start, dest, 1)], MovementType.Walk);
        var path2 = new MovementPath([new PathSegment(start, dest, 100) ], MovementType.Walk); // Different cost/segments but the same endpoints
        
        path1.Equals(path2).ShouldBeTrue("Paths with same endpoints should be equal in this model");
        path1.GetHashCode().ShouldBe(path2.GetHashCode());
    }
    
    [Fact]
    public void Clear_ShouldRemoveAllPaths()
    {
        var start = new HexPosition(1, 1, HexDirection.Top);
        var dest = new HexPosition(2, 2, HexDirection.Bottom);
        
        var segments = new List<PathSegment> { new(start, dest, 1) };
        var path = new MovementPath(segments, MovementType.Walk);
        
        _sut.Add(path);
        
        _sut.Clear();
        _sut.Get(start, dest, false).ShouldBeNull();
    }
    
    [Fact]
    public void Cache_ShouldDifferentiateByMaxLevelChange()
    {
        var start = new HexPosition(1, 1, HexDirection.Top);
        var dest = new HexPosition(2, 2, HexDirection.Bottom);
        
        // Path with no level constraint
        var noConstraintSegments = new List<PathSegment> { new(start, dest, 2) };
        var noConstraintPath = new MovementPath(noConstraintSegments, MovementType.Walk, null);
        
        // Path with maxLevelChange = 1
        var level1Segments = new List<PathSegment> { new(start, dest, 3) };
        var level1Path = new MovementPath(level1Segments, MovementType.Walk, 1);
        
        // Path with maxLevelChange = 2
        var level2Segments = new List<PathSegment> { new(start, dest, 4) };
        var level2Path = new MovementPath(level2Segments, MovementType.Walk, 2);
        
        _sut.Add(noConstraintPath);
        _sut.Add(level1Path);
        _sut.Add(level2Path);
        
        _sut.Get(start, dest, false, null).ShouldBe(noConstraintPath);
        _sut.Get(start, dest, false, 1).ShouldBe(level1Path);
        _sut.Get(start, dest, false, 2).ShouldBe(level2Path);
    }
    
    [Fact]
    public void Get_ShouldReturnNull_WhenMaxLevelChangeDoesNotMatch()
    {
        var start = new HexPosition(1, 1, HexDirection.Top);
        var dest = new HexPosition(2, 2, HexDirection.Bottom);
        
        // Only add path with maxLevelChange = 1
        var segments = new List<PathSegment> { new(start, dest, 2) };
        var path = new MovementPath(segments, MovementType.Walk, 1);
        
        _sut.Add(path);
        
        // Should not find path with different maxLevelChange
        _sut.Get(start, dest, false, null).ShouldBeNull();
        _sut.Get(start, dest, false, 2).ShouldBeNull();
        
        // Should find path with matching maxLevelChange
        _sut.Get(start, dest, false, 1).ShouldBe(path);
    }
    
    [Fact]
    public void GetHashCode_And_Equals_ShouldDifferentiateByMaxLevelChange()
    {
        var start = new HexPosition(1, 1, HexDirection.Top);
        var dest = new HexPosition(2, 2, HexDirection.Bottom);
        
        var pathNoConstraint = new MovementPath([new PathSegment(start, dest, 1)], MovementType.Walk, null);
        var pathLevel1 = new MovementPath([new PathSegment(start, dest, 1)], MovementType.Walk, 1);
        var pathLevel2 = new MovementPath([new PathSegment(start, dest, 1)], MovementType.Walk, 2);
        
        pathNoConstraint.Equals(pathLevel1).ShouldBeFalse("Paths with different maxLevelChange should not be equal");
        pathLevel1.Equals(pathLevel2).ShouldBeFalse("Paths with different maxLevelChange should not be equal");
        
        pathNoConstraint.GetHashCode().ShouldNotBe(pathLevel1.GetHashCode());
        pathLevel1.GetHashCode().ShouldNotBe(pathLevel2.GetHashCode());
    }
}
