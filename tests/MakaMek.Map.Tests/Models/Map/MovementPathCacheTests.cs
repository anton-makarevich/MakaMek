using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Map;

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
}
