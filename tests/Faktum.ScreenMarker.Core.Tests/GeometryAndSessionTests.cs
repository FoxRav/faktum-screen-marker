using Faktum.ScreenMarker.Core.Application;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Geometry;
using Faktum.ScreenMarker.Core.History;
using Faktum.ScreenMarker.Core.Simplification;

namespace Faktum.ScreenMarker.Core.Tests;

public class GeometryTests
{
    [Fact]
    public void NormalizeRectWorksForAllDragDirections()
    {
        var rect = GeometryHelpers.NormalizeRect(new Point2D(10, 20), new Point2D(0, 0));
        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(10, rect.Width);
        Assert.Equal(20, rect.Height);
    }

    [Fact]
    public void ShiftConstraintProducesSquare()
    {
        var rect = GeometryHelpers.NormalizeRect(new Point2D(0, 0), new Point2D(30, 10), constrainSquare: true);
        Assert.Equal(30, rect.Width);
        Assert.Equal(30, rect.Height);
    }

    [Fact]
    public void SimplificationPreservesEndpoints()
    {
        var points = new List<Point2D>
        {
            new(0, 0),
            new(1, 0.1),
            new(2, 0),
            new(10, 0),
        };
        var simplified = RamerDouglasPeucker.Simplify(points, tolerance: 0.5);
        Assert.Equal(new Point2D(0, 0), simplified[0]);
        Assert.Equal(new Point2D(10, 0), simplified[^1]);
    }

    [Fact]
    public void HitTestingFindsTopmostObject()
    {
        const string monitor = @"\\.\DISPLAY1";
        var objects = new DrawingObject[]
        {
            new LineAnnotation(1, monitor, StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(100, 0)),
            new LineAnnotation(2, monitor, StrokeStyle.DefaultPen, new Point2D(0, 5), new Point2D(100, 5)),
        };
        var hit = HitTesting.FindTopmostHit(objects, new Point2D(50, 5));
        Assert.Equal(2, hit);
    }
}

public class DrawingSessionTests
{
    private const string MonitorA = @"\\.\DISPLAY1";
    private const string MonitorB = @"\\.\DISPLAY2";

    [Fact]
    public void UndoRedoAndClearWork()
    {
        using var session = new DrawingSession();
        session.BeginPreview(MonitorA, new LineAnnotation(session.AllocateId(), MonitorA, StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(10, 10)));
        session.CommitPreview();
        Assert.Single(session.Objects);
        session.Undo();
        Assert.Empty(session.Objects);
        session.Redo();
        Assert.Single(session.Objects);
        session.ClearAllWithHistory();
        Assert.Empty(session.Objects);
        session.Undo();
        Assert.Single(session.Objects);
    }

    [Fact]
    public void DeactivationClearsSession()
    {
        var session = new DrawingSession();
        session.BeginPreview(MonitorA, new LineAnnotation(session.AllocateId(), MonitorA, StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(10, 10)));
        session.CommitPreview();
        session.Dispose();
        Assert.Empty(session.Objects);
    }

    [Fact]
    public void ObjectsAreScopedToMonitor()
    {
        using var session = new DrawingSession();
        session.BeginPreview(MonitorA, new LineAnnotation(session.AllocateId(), MonitorA, StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(10, 10)));
        session.CommitPreview();
        session.BeginPreview(MonitorB, new LineAnnotation(session.AllocateId(), MonitorB, StrokeStyle.DefaultPen, new Point2D(20, 20), new Point2D(30, 30)));
        session.CommitPreview();

        Assert.Single(session.GetObjectsForMonitor(MonitorA));
        Assert.Single(session.GetObjectsForMonitor(MonitorB));
        Assert.Equal(2, session.Objects.Count);
    }

    [Fact]
    public void EraseAtOnlyRemovesFromTargetMonitor()
    {
        using var session = new DrawingSession();
        session.BeginPreview(MonitorA, new LineAnnotation(session.AllocateId(), MonitorA, StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(100, 0)));
        session.CommitPreview();
        session.BeginPreview(MonitorB, new LineAnnotation(session.AllocateId(), MonitorB, StrokeStyle.DefaultPen, new Point2D(0, 5), new Point2D(100, 5)));
        session.CommitPreview();

        Assert.True(session.EraseAt(MonitorA, new Point2D(50, 0)));
        Assert.Empty(session.GetObjectsForMonitor(MonitorA));
        Assert.Single(session.GetObjectsForMonitor(MonitorB));
    }

    [Fact]
    public void ChangedEventFiresOnCommitUndoRedoAndClear()
    {
        using var session = new DrawingSession();
        var changes = 0;
        session.Changed += () => changes++;

        session.BeginPreview(MonitorA, new LineAnnotation(session.AllocateId(), MonitorA, StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(10, 10)));
        session.CommitPreview();
        session.Undo();
        session.Redo();
        session.ClearAllWithHistory();

        Assert.True(changes >= 4);
    }
}

public class DrawingHistoryTests
{
    private const string Monitor = @"\\.\DISPLAY1";

    [Fact]
    public void TrimDiscardsOldestWithoutDuplicatingObjects()
    {
        using var session = new DrawingSession();
        const int totalCommits = DrawingHistory.MaxCommandCount + 50;
        for (var i = 0; i < totalCommits; i++)
        {
            session.BeginPreview(Monitor, new LineAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(i, 0), new Point2D(i + 1, 1)));
            session.CommitPreview();
        }

        Assert.Equal(DrawingHistory.MaxCommandCount, session.History.UndoCount);
        Assert.Equal(totalCommits, session.Objects.Count);
        var ids = session.Objects.Select(o => o.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public void EstimatedBytesDecreasesWhenOldestCommandIsTrimmed()
    {
        using var session = new DrawingSession();
        for (var i = 0; i < DrawingHistory.MaxCommandCount + 1; i++)
        {
            session.BeginPreview(Monitor, new TextAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(0, 0), new string('x', 200), 16));
            session.CommitPreview();
        }

        Assert.True(session.History.EstimatedBytes <= DrawingHistory.MaxEstimatedBytes);
        Assert.Equal(DrawingHistory.MaxCommandCount, session.History.UndoCount);
        Assert.Equal(DrawingHistory.MaxCommandCount + 1, session.Objects.Count);
    }

    [Fact]
    public void NewBranchSubtractsDiscardedRedoBytes()
    {
        using var session = new DrawingSession();
        session.BeginPreview(Monitor, new TextAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(0, 0), new string('a', 100), 16));
        session.CommitPreview();
        session.BeginPreview(Monitor, new TextAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(1, 1), new string('b', 100), 16));
        session.CommitPreview();
        session.Undo();
        session.BeginPreview(Monitor, new TextAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(2, 2), new string('c', 100), 16));
        session.CommitPreview();
        Assert.Equal(session.History.ComputeRetainedEstimate(), session.History.EstimatedBytes);
    }

    [Fact]
    public void UndoRestoresOriginalInsertionIndex()
    {
        using var session = new DrawingSession();
        session.BeginPreview(Monitor, new LineAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(1, 1)));
        session.CommitPreview();
        session.BeginPreview(Monitor, new LineAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(2, 2), new Point2D(3, 3)));
        session.CommitPreview();
        var secondId = session.Objects[1].Id;
        session.Undo();
        Assert.Single(session.Objects);
        session.Redo();
        Assert.Equal(2, session.Objects.Count);
        Assert.Equal(secondId, session.Objects[1].Id);
    }

    [Fact]
    public void EraserUndoRestoresOriginalZOrder()
    {
        using var session = new DrawingSession();
        session.BeginPreview(Monitor, new LineAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(10, 0)));
        session.CommitPreview();
        session.BeginPreview(Monitor, new LineAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(0, 5), new Point2D(10, 5)));
        session.CommitPreview();
        var bottomId = session.Objects[0].Id;
        var topId = session.Objects[1].Id;
        session.EraseAt(Monitor, new Point2D(5, 5));
        Assert.Equal(bottomId, session.Objects[0].Id);
        session.Undo();
        Assert.Equal(bottomId, session.Objects[0].Id);
        Assert.Equal(topId, session.Objects[1].Id);
    }

    [Fact]
    public void EstimatedBytesNeverNegativeAfterClearAndUndo()
    {
        using var session = new DrawingSession();
        session.BeginPreview(Monitor, new LineAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(10, 10)));
        session.CommitPreview();
        session.ClearAllWithHistory();
        session.Undo();
        Assert.True(session.History.EstimatedBytes >= 0);
    }

    [Fact]
    public void ExecuteIncreasesRetainedEstimate()
    {
        using var session = new DrawingSession();
        session.BeginPreview(Monitor, new TextAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(0, 0), new string('a', 50), 16));
        session.CommitPreview();
        var expected = session.History.ComputeRetainedEstimate();
        Assert.Equal(expected, session.History.EstimatedBytes);
        Assert.True(expected > 0);
    }

    [Fact]
    public void UndoAndRedoKeepRetainedEstimateUnchanged()
    {
        using var session = new DrawingSession();
        session.BeginPreview(Monitor, new TextAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(0, 0), new string('a', 50), 16));
        session.CommitPreview();
        var afterExecute = session.History.EstimatedBytes;
        session.Undo();
        Assert.Equal(afterExecute, session.History.EstimatedBytes);
        session.Redo();
        Assert.Equal(afterExecute, session.History.EstimatedBytes);
    }

    [Fact]
    public void NewBranchSubtractsDiscardedRedoExactlyOnce()
    {
        using var session = new DrawingSession();
        session.BeginPreview(Monitor, new TextAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(0, 0), new string('a', 100), 16));
        session.CommitPreview();
        session.BeginPreview(Monitor, new TextAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(1, 1), new string('b', 100), 16));
        session.CommitPreview();
        session.Undo();
        var withRedo = session.History.EstimatedBytes;
        session.BeginPreview(Monitor, new TextAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(2, 2), new string('c', 100), 16));
        session.CommitPreview();
        Assert.Equal(withRedo, session.History.EstimatedBytes);
        Assert.Equal(session.History.ComputeRetainedEstimate(), session.History.EstimatedBytes);
    }

    [Fact]
    public void ClearResetsEstimateToZero()
    {
        using var session = new DrawingSession();
        session.BeginPreview(Monitor, new LineAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(10, 10)));
        session.CommitPreview();
        session.History.Clear();
        Assert.Equal(0, session.History.EstimatedBytes);
        Assert.Equal(0, session.History.UndoCount);
        Assert.Equal(0, session.History.RedoCount);
    }

    [Fact]
    public void TrimRemovesOldestEstimateExactlyOnce()
    {
        using var session = new DrawingSession();
        for (var i = 0; i < DrawingHistory.MaxCommandCount + 1; i++)
        {
            session.BeginPreview(Monitor, new TextAnnotation(session.AllocateId(), Monitor, StrokeStyle.DefaultPen, new Point2D(i, 0), new string('x', 20), 16));
            session.CommitPreview();
        }

        Assert.Equal(DrawingHistory.MaxCommandCount, session.History.UndoCount);
        Assert.Equal(session.History.ComputeRetainedEstimate(), session.History.EstimatedBytes);
        Assert.True(session.History.EstimatedBytes <= DrawingHistory.MaxEstimatedBytes);
    }
}

public class ApplicationStateCoordinatorTests
{
    [Fact]
    public void RapidDoubleToggleEndsIdle()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();
        coordinator.RequestHotkeyToggle();
        coordinator.RequestHotkeyToggle();
        var afterActivation = coordinator.MarkActivationSucceeded();
        Assert.Equal(ApplicationState.Deactivating, afterActivation.NewState);
        Assert.Equal(ApplicationTransitionAction.Deactivate, afterActivation.Action);
        var afterDeactivation = coordinator.MarkDeactivationComplete();
        Assert.Equal(ApplicationState.Idle, afterDeactivation.NewState);
        Assert.Equal(ApplicationTransitionAction.None, afterDeactivation.Action);
    }

    [Fact]
    public void RapidTripleToggleEndsActive()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();
        coordinator.RequestHotkeyToggle();
        coordinator.RequestHotkeyToggle();
        coordinator.RequestHotkeyToggle();
        var afterActivation = coordinator.MarkActivationSucceeded();
        Assert.Equal(ApplicationState.Active, afterActivation.NewState);
        Assert.Equal(ApplicationTransitionAction.None, afterActivation.Action);
    }

    [Fact]
    public void ParitySequenceFromIdleMatchesExpectedStates()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();

        for (var count = 1; count <= 10; count++)
        {
            coordinator = new ApplicationStateCoordinator();
            coordinator.MarkStarted();
            for (var i = 0; i < count; i++)
            {
                coordinator.RequestHotkeyToggle();
            }

            var expectedActive = count % 2 == 1;
            var transition = SimulateQueuedTransitions(coordinator);
            Assert.Equal(expectedActive ? ApplicationState.Active : ApplicationState.Idle, transition);
        }
    }

    [Fact]
    public void ToggleDuringActivatingDoesNotReactivate()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();
        var first = coordinator.RequestHotkeyToggle();
        Assert.Equal(ApplicationTransitionAction.Activate, first.Action);
        var second = coordinator.RequestHotkeyToggle();
        Assert.Equal(ApplicationTransitionAction.None, second.Action);
        Assert.Equal(ApplicationState.Activating, second.NewState);
    }

    [Fact]
    public void ToggleDuringDeactivatingDoesNotRedeactivate()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();
        coordinator.RequestHotkeyToggle();
        coordinator.MarkActivationSucceeded();
        var first = coordinator.RequestHotkeyToggle();
        Assert.Equal(ApplicationTransitionAction.Deactivate, first.Action);
        var second = coordinator.RequestHotkeyToggle();
        Assert.Equal(ApplicationTransitionAction.None, second.Action);
        Assert.Equal(ApplicationState.Deactivating, second.NewState);
    }

    [Fact]
    public void ToggleCoalescesDuringActivation()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();
        coordinator.RequestHotkeyToggle();
        coordinator.RequestHotkeyToggle();
        var result = coordinator.MarkActivationSucceeded();
        Assert.Equal(ApplicationState.Deactivating, result.NewState);
    }

    [Fact]
    public void ToggleCoalescesDuringDeactivation()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();
        coordinator.RequestHotkeyToggle();
        coordinator.MarkActivationSucceeded();
        coordinator.RequestHotkeyToggle();
        coordinator.RequestHotkeyToggle();
        var result = coordinator.MarkDeactivationComplete();
        Assert.Equal(ApplicationState.Activating, result.NewState);
        Assert.Equal(ApplicationTransitionAction.Activate, result.Action);
    }

    [Fact]
    public void IdempotentDeactivationFromIdle()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();
        var result = coordinator.RequestExplicitDeactivate();
        Assert.Equal(ApplicationState.Idle, result.NewState);
        Assert.Equal(ApplicationTransitionAction.None, result.Action);
    }

    [Fact]
    public void IdempotentActivationFromActive()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();
        coordinator.RequestHotkeyToggle();
        coordinator.MarkActivationSucceeded();
        var result = coordinator.RequestExplicitActivate();
        Assert.Equal(ApplicationState.Active, result.NewState);
        Assert.Equal(ApplicationTransitionAction.None, result.Action);
    }

    [Fact]
    public void ActivationFailureReturnsIdle()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();
        coordinator.RequestHotkeyToggle();
        var result = coordinator.MarkActivationFailed("failed");
        Assert.Equal(ApplicationState.Idle, result.NewState);
        Assert.Equal(ApplicationTransitionAction.None, result.Action);
        Assert.True(result.ActivationFailed);
    }

    [Fact]
    public void MarkStoppingClearsPendingParity()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();
        coordinator.RequestHotkeyToggle();
        coordinator.RequestHotkeyToggle();
        coordinator.MarkStopping();
        var result = coordinator.MarkActivationSucceeded();
        Assert.Equal(ApplicationState.Stopping, result.NewState);
        Assert.Equal(ApplicationTransitionAction.None, result.Action);
    }

    private static ApplicationState SimulateQueuedTransitions(ApplicationStateCoordinator coordinator)
    {
        while (true)
        {
            var state = coordinator.State;
            switch (state)
            {
                case ApplicationState.Idle:
                case ApplicationState.Active:
                    return state;
                case ApplicationState.Activating:
                    coordinator.MarkActivationSucceeded();
                    break;
                case ApplicationState.Deactivating:
                    coordinator.MarkDeactivationComplete();
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected state {state}");
            }
        }
    }
}

public class DrawingLifecycleOrchestratorTests
{
    [Fact]
    public void HotkeyToggleThroughOrchestratorActivatesThenDeactivates()
    {
        var activations = 0;
        var deactivations = 0;
        var orchestrator = new DrawingLifecycleOrchestrator(
            new ApplicationStateCoordinator(),
            () => activations++,
            () => deactivations++);

        orchestrator.MarkStarted();
        orchestrator.RequestHotkeyToggle();
        Assert.Equal(1, activations);
        orchestrator.NotifyActivationSucceeded();
        orchestrator.RequestHotkeyToggle();
        Assert.Equal(1, deactivations);
    }

    [Fact]
    public void ExplicitActivateIsIdempotentWhileActive()
    {
        var activations = 0;
        var orchestrator = new DrawingLifecycleOrchestrator(
            new ApplicationStateCoordinator(),
            () => activations++,
            () => { });

        orchestrator.MarkStarted();
        orchestrator.RequestExplicitActivate();
        orchestrator.NotifyActivationSucceeded();
        orchestrator.RequestExplicitActivate();
        Assert.Equal(1, activations);
        Assert.Equal(ApplicationState.Active, orchestrator.State);
    }

    [Fact]
    public void ToggleDuringActivatingDoesNotDoubleActivate()
    {
        var activations = 0;
        var orchestrator = new DrawingLifecycleOrchestrator(
            new ApplicationStateCoordinator(),
            () => activations++,
            () => { });

        orchestrator.MarkStarted();
        orchestrator.RequestHotkeyToggle();
        orchestrator.RequestHotkeyToggle();
        Assert.Equal(1, activations);
    }

    [Fact]
    public void ToggleDuringDeactivatingDoesNotDoubleDeactivate()
    {
        var deactivations = 0;
        var orchestrator = new DrawingLifecycleOrchestrator(
            new ApplicationStateCoordinator(),
            () => { },
            () => deactivations++);

        orchestrator.MarkStarted();
        orchestrator.RequestHotkeyToggle();
        orchestrator.NotifyActivationSucceeded();
        orchestrator.RequestHotkeyToggle();
        orchestrator.RequestHotkeyToggle();
        Assert.Equal(1, deactivations);
    }

    [Fact]
    public void ExplicitActivateWhileActivatingDoesNotDoubleActivate()
    {
        var activations = 0;
        var orchestrator = new DrawingLifecycleOrchestrator(
            new ApplicationStateCoordinator(),
            () => activations++,
            () => { });

        orchestrator.MarkStarted();
        orchestrator.RequestHotkeyToggle();
        orchestrator.RequestExplicitActivate();
        Assert.Equal(1, activations);
    }

    [Fact]
    public void ExplicitDeactivateWhileDeactivatingDoesNotDoubleDeactivate()
    {
        var deactivations = 0;
        var orchestrator = new DrawingLifecycleOrchestrator(
            new ApplicationStateCoordinator(),
            () => { },
            () => deactivations++);

        orchestrator.MarkStarted();
        orchestrator.RequestHotkeyToggle();
        orchestrator.NotifyActivationSucceeded();
        orchestrator.RequestHotkeyToggle();
        orchestrator.RequestExplicitDeactivate();
        Assert.Equal(1, deactivations);
    }
}
