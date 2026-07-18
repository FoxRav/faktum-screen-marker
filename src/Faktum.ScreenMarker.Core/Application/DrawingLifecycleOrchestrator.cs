namespace Faktum.ScreenMarker.Core.Application;

/// <summary>
/// Connects application-state transitions to activation/deactivation side effects.
/// </summary>
public sealed class DrawingLifecycleOrchestrator
{
    private readonly ApplicationStateCoordinator _state;
    private readonly Action _activateDrawing;
    private readonly Action _deactivateDrawing;

    public DrawingLifecycleOrchestrator(
        ApplicationStateCoordinator state,
        Action activateDrawing,
        Action deactivateDrawing)
    {
        _state = state;
        _activateDrawing = activateDrawing;
        _deactivateDrawing = deactivateDrawing;
    }

    public ApplicationState State => _state.State;

    public bool IsDrawingActive => _state.IsDrawingActive;

    public void MarkStarted() => _state.MarkStarted();

    public void RequestHotkeyToggle() => ConsumeTransition(_state.RequestHotkeyToggle());

    public void RequestExplicitActivate() => ConsumeTransition(_state.RequestExplicitActivate());

    public void RequestExplicitDeactivate() => ConsumeTransition(_state.RequestExplicitDeactivate());

    public void NotifyActivationSucceeded() => ConsumeTransition(_state.MarkActivationSucceeded());

    public void NotifyActivationFailed(string message) => ConsumeTransition(_state.MarkActivationFailed(message));

    public void NotifyDeactivationComplete() => ConsumeTransition(_state.MarkDeactivationComplete());

    public void NotifyDisplayRebuildFailed() => ConsumeTransition(_state.MarkDisplayRebuildFailed());

    public void NotifyFaultRecoverable(string message) => ConsumeTransition(_state.MarkFaultRecoverable(message));

    public void MarkStopping() => _state.MarkStopping();

    private void ConsumeTransition(ApplicationTransitionResult transition)
    {
        switch (transition.Action)
        {
            case ApplicationTransitionAction.Activate:
                _activateDrawing();
                break;
            case ApplicationTransitionAction.Deactivate:
                _deactivateDrawing();
                break;
            case ApplicationTransitionAction.None:
                break;
            default:
                throw new InvalidOperationException($"Unhandled transition action: {transition.Action}");
        }
    }
}
