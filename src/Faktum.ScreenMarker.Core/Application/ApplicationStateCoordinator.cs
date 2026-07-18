namespace Faktum.ScreenMarker.Core.Application;

public enum ApplicationState
{
    Starting,
    Idle,
    Activating,
    Active,
    Deactivating,
    FaultedRecoverable,
    Stopping,
}

public enum ApplicationTransitionAction
{
    None,
    Activate,
    Deactivate,
}

public readonly record struct ApplicationTransitionResult(
    ApplicationState NewState,
    ApplicationTransitionAction Action,
    bool SessionCreated,
    bool SessionDisposed,
    bool ActivationFailed,
    string? UserMessage);

public sealed class ApplicationStateCoordinator
{
    private readonly object _gate = new();
    private ApplicationState _state = ApplicationState.Starting;
    private bool _pendingToggleParity;

    public ApplicationState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public bool IsDrawingActive => State == ApplicationState.Active;

    public void MarkStarted()
    {
        lock (_gate)
        {
            _state = ApplicationState.Idle;
        }
    }

    public void MarkStopping()
    {
        lock (_gate)
        {
            _state = ApplicationState.Stopping;
            _pendingToggleParity = false;
        }
    }

    public ApplicationTransitionResult RequestHotkeyToggle()
    {
        lock (_gate)
        {
            return _state switch
            {
                ApplicationState.Idle => BeginActivation(),
                ApplicationState.Active => BeginDeactivation(),
                ApplicationState.Activating or ApplicationState.Deactivating => FlipPendingToggleParity(),
                ApplicationState.FaultedRecoverable => BeginActivation(),
                _ => NoTransition(),
            };
        }
    }

    public ApplicationTransitionResult RequestExplicitActivate()
    {
        lock (_gate)
        {
            return _state switch
            {
                ApplicationState.Idle or ApplicationState.FaultedRecoverable => BeginActivation(),
                _ => NoTransition(),
            };
        }
    }

    public ApplicationTransitionResult RequestExplicitDeactivate()
    {
        lock (_gate)
        {
            return _state == ApplicationState.Active ? BeginDeactivation() : NoTransition();
        }
    }

    private ApplicationTransitionResult BeginActivation()
    {
        _state = ApplicationState.Activating;
        return new ApplicationTransitionResult(_state, ApplicationTransitionAction.Activate, false, false, false, null);
    }

    private ApplicationTransitionResult BeginDeactivation()
    {
        _state = ApplicationState.Deactivating;
        return new ApplicationTransitionResult(_state, ApplicationTransitionAction.Deactivate, false, false, false, null);
    }

    private ApplicationTransitionResult FlipPendingToggleParity()
    {
        _pendingToggleParity = !_pendingToggleParity;
        return new ApplicationTransitionResult(_state, ApplicationTransitionAction.None, false, false, false, null);
    }

    private ApplicationTransitionResult NoTransition() =>
        new(_state, ApplicationTransitionAction.None, false, false, false, null);

    public ApplicationTransitionResult MarkActivationSucceeded()
    {
        lock (_gate)
        {
            if (_state == ApplicationState.Stopping)
            {
                return NoTransition();
            }

            _state = ApplicationState.Active;
            if (_pendingToggleParity)
            {
                _pendingToggleParity = false;
                _state = ApplicationState.Deactivating;
                return new ApplicationTransitionResult(_state, ApplicationTransitionAction.Deactivate, true, false, false, null);
            }

            _pendingToggleParity = false;
            return new ApplicationTransitionResult(_state, ApplicationTransitionAction.None, true, false, false, null);
        }
    }

    public ApplicationTransitionResult MarkActivationFailed(string message)
    {
        lock (_gate)
        {
            _state = ApplicationState.Idle;
            _pendingToggleParity = false;
            return new ApplicationTransitionResult(_state, ApplicationTransitionAction.None, false, false, true, message);
        }
    }

    public ApplicationTransitionResult MarkDeactivationComplete()
    {
        lock (_gate)
        {
            if (_state == ApplicationState.Stopping)
            {
                return NoTransition();
            }

            _state = ApplicationState.Idle;
            if (_pendingToggleParity)
            {
                _pendingToggleParity = false;
                _state = ApplicationState.Activating;
                return new ApplicationTransitionResult(_state, ApplicationTransitionAction.Activate, false, true, false, null);
            }

            _pendingToggleParity = false;
            return new ApplicationTransitionResult(_state, ApplicationTransitionAction.None, false, true, false, null);
        }
    }

    public ApplicationTransitionResult MarkDisplayRebuildFailed()
    {
        lock (_gate)
        {
            if (_state != ApplicationState.Active)
            {
                return new ApplicationTransitionResult(_state, ApplicationTransitionAction.None, false, false, false, null);
            }

            _state = ApplicationState.FaultedRecoverable;
            _pendingToggleParity = false;
            return new ApplicationTransitionResult(_state, ApplicationTransitionAction.None, false, true, false, "Display rebuild failed.");
        }
    }

    public ApplicationTransitionResult MarkFaultRecoverable(string message)
    {
        lock (_gate)
        {
            _state = ApplicationState.FaultedRecoverable;
            _pendingToggleParity = false;
            return new ApplicationTransitionResult(_state, ApplicationTransitionAction.None, false, true, false, message);
        }
    }
}
