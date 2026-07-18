using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Simplification;

namespace Faktum.ScreenMarker.Core.Tests;

public class RamerDouglasPeuckerTests
{
    [Fact]
    public void RecursiveSplitBothSidesDoesNotThrowAndPreservesEndpoints()
    {
        var points = new List<Point2D>();
        for (var x = 0; x <= 20; x++)
        {
            points.Add(new Point2D(x, x % 2 == 0 ? 0 : 8));
        }

        var simplified = RamerDouglasPeucker.Simplify(points, tolerance: 1.0);
        Assert.Equal(points[0], simplified[0]);
        Assert.Equal(points[^1], simplified[^1]);
        Assert.True(simplified.Count >= 3);
    }

    [Fact]
    public void ComplexZigzag1000PointsSimplifiesWithoutException()
    {
        var points = new List<Point2D>(1000);
        for (var i = 0; i < 1000; i++)
        {
            points.Add(new Point2D(i, i % 2 == 0 ? 0 : 12));
        }

        var simplified = RamerDouglasPeucker.Simplify(points, tolerance: 2.0);
        Assert.Equal(points[0], simplified[0]);
        Assert.Equal(points[^1], simplified[^1]);
        Assert.True(simplified.Count < points.Count);
    }

    [Fact]
    public void LongCurvedSine10000PointsDoesNotStackOverflow()
    {
        var points = new List<Point2D>(10_000);
        for (var i = 0; i < 10_000; i++)
        {
            var t = i / 100.0;
            points.Add(new Point2D(i, Math.Sin(t) * 50));
        }

        var simplified = RamerDouglasPeucker.Simplify(points, tolerance: 1.5);
        Assert.Equal(points[0], simplified[0]);
        Assert.Equal(points[^1], simplified[^1]);
        Assert.True(simplified.Count < points.Count);
    }

    [Fact]
    public void DuplicatePointsAreHandledSafely()
    {
        var allSame = new[] { new Point2D(1, 1), new Point2D(1, 1), new Point2D(1, 1) };
        var simplifiedSame = RamerDouglasPeucker.Simplify(allSame, tolerance: 1.0);
        Assert.Equal(2, simplifiedSame.Count);
        Assert.Equal(new Point2D(1, 1), simplifiedSame[0]);
        Assert.Equal(new Point2D(1, 1), simplifiedSame[^1]);

        var repeatedEnds = new[]
        {
            new Point2D(0, 0),
            new Point2D(0, 0),
            new Point2D(5, 5),
            new Point2D(10, 10),
            new Point2D(10, 10),
        };
        var simplifiedEnds = RamerDouglasPeucker.Simplify(repeatedEnds, tolerance: 1.0);
        Assert.Equal(new Point2D(0, 0), simplifiedEnds[0]);
        Assert.Equal(new Point2D(10, 10), simplifiedEnds[^1]);

        var repeatedInternal = new[]
        {
            new Point2D(0, 0),
            new Point2D(5, 5),
            new Point2D(5, 5),
            new Point2D(5, 5),
            new Point2D(10, 0),
        };
        var simplifiedInternal = RamerDouglasPeucker.Simplify(repeatedInternal, tolerance: 0.5);
        Assert.Equal(new Point2D(0, 0), simplifiedInternal[0]);
        Assert.Equal(new Point2D(10, 0), simplifiedInternal[^1]);
    }

    [Fact]
    public void ZeroOrOnePointReturnsCopy()
    {
        Assert.Empty(RamerDouglasPeucker.Simplify(Array.Empty<Point2D>()));
        var single = new[] { new Point2D(3, 4) };
        var simplified = RamerDouglasPeucker.Simplify(single);
        Assert.Single(simplified);
        Assert.Equal(single[0], simplified[0]);
    }

    [Fact]
    public void TwoPointsKeepsBoth()
    {
        var points = new[] { new Point2D(0, 0), new Point2D(10, 10) };
        var simplified = RamerDouglasPeucker.Simplify(points);
        Assert.Equal(2, simplified.Count);
    }

    [Fact]
    public void InvalidToleranceIsRejected()
    {
        var points = new[] { new Point2D(0, 0), new Point2D(1, 1), new Point2D(2, 0) };
        Assert.Throws<ArgumentOutOfRangeException>(() => RamerDouglasPeucker.Simplify(points, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => RamerDouglasPeucker.Simplify(points, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => RamerDouglasPeucker.Simplify(points, -1));
    }

    [Fact]
    public void InputCollectionIsNotMutated()
    {
        var points = new List<Point2D>
        {
            new(0, 0),
            new(2, 8),
            new(4, 0),
            new(6, 8),
            new(8, 0),
        };
        var originalCount = points.Count;
        _ = RamerDouglasPeucker.Simplify(points, tolerance: 1.0);
        Assert.Equal(originalCount, points.Count);
    }
}

public class FreehandInputValidationTests
{
    [Fact]
    public void RejectsNonFiniteCoordinates()
    {
        Assert.False(FreehandInputValidation.TryPrepareForCommit(
            [new Point2D(0, 0), new Point2D(double.NaN, 1)],
            out _));
        Assert.False(FreehandInputValidation.TryPrepareForCommit(
            [new Point2D(0, 0), new Point2D(1, double.PositiveInfinity)],
            out _));
    }

    [Fact]
    public void RemovesNearExactConsecutiveDuplicates()
    {
        Assert.True(FreehandInputValidation.TryPrepareForCommit(
            [new Point2D(0, 0), new Point2D(0, 0), new Point2D(10, 0)],
            out var prepared));
        Assert.Equal(2, prepared.Length);
        Assert.Equal(new Point2D(0, 0), prepared[0]);
        Assert.Equal(new Point2D(10, 0), prepared[1]);
    }

    [Fact]
    public void RejectsSinglePointAfterDedupe()
    {
        Assert.False(FreehandInputValidation.TryPrepareForCommit(
            [new Point2D(1, 1), new Point2D(1, 1)],
            out _));
    }
}

public class FreehandSessionRegressionTests
{
    private const string MonitorLaptop = @"\\.\DISPLAY1";
    private const string MonitorBenQ = @"\\.\BenQ EX2710Q";

    private static List<Point2D> BuildComplexStroke(int seed)
    {
        var points = new List<Point2D>(200);
        for (var i = 0; i < 200; i++)
        {
            var x = i + seed;
            var y = Math.Sin(i / 10.0 + seed) * 40 + (i % 2 == 0 ? 0 : 6);
            points.Add(new Point2D(x, y));
        }

        return points;
    }

    [Fact]
    public void HundredConsecutiveComplexFreehandCommitsHaveUniqueIdsAndUndoRedo()
    {
        using var session = new DrawingSession();
        var ids = new HashSet<int>();
        for (var i = 0; i < 100; i++)
        {
            var id = session.AllocateId();
            session.BeginPreview(MonitorLaptop, new FreehandStroke(id, MonitorLaptop, StrokeStyle.DefaultPen, BuildComplexStroke(i)));
            Assert.True(session.CommitPreview());
            ids.Add(id);
        }

        Assert.Equal(100, session.Objects.Count);
        Assert.Equal(100, ids.Count);
        session.Undo();
        Assert.Equal(99, session.Objects.Count);
        session.Redo();
        Assert.Equal(100, session.Objects.Count);
    }

    [Fact]
    public void AlternatingMonitorsIsolateCommittedObjects()
    {
        using var session = new DrawingSession();
        for (var i = 0; i < 20; i++)
        {
            var monitor = i % 2 == 0 ? MonitorLaptop : MonitorBenQ;
            session.BeginPreview(
                monitor,
                new FreehandStroke(session.AllocateId(), monitor, StrokeStyle.DefaultPen, BuildComplexStroke(i)));
            Assert.True(session.CommitPreview());
        }

        Assert.Equal(10, session.GetObjectsForMonitor(MonitorLaptop).Count);
        Assert.Equal(10, session.GetObjectsForMonitor(MonitorBenQ).Count);
        Assert.All(session.GetObjectsForMonitor(MonitorLaptop), o => Assert.Equal(MonitorLaptop, o.MonitorDeviceName));
        Assert.All(session.GetObjectsForMonitor(MonitorBenQ), o => Assert.Equal(MonitorBenQ, o.MonitorDeviceName));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(100)]
    public void SequentialCommitsDoNotReusePreviewState(int strokeCount)
    {
        using var session = new DrawingSession();
        for (var i = 0; i < strokeCount; i++)
        {
            session.BeginPreview(
                MonitorLaptop,
                new FreehandStroke(session.AllocateId(), MonitorLaptop, StrokeStyle.DefaultPen, BuildComplexStroke(i)));
            Assert.NotNull(session.PreviewObject);
            Assert.True(session.CommitPreview());
            Assert.Null(session.PreviewObject);
        }

        Assert.Equal(strokeCount, session.Objects.Count);
    }

    [Fact]
    public void MalformedPreviewIsNotCommitted()
    {
        using var session = new DrawingSession();
        session.BeginPreview(
            MonitorLaptop,
            new FreehandStroke(session.AllocateId(), MonitorLaptop, StrokeStyle.DefaultPen, [new Point2D(0, 0)]));
        Assert.False(session.CommitPreview());
        Assert.Empty(session.Objects);
        Assert.Null(session.PreviewObject);
    }
}
