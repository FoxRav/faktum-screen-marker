namespace Faktum.ScreenMarker.App.Overlays;

public interface IDebounceScheduler : IDisposable
{
    void Schedule(int delayMilliseconds, Action callback);

    void CancelPending();
}

public sealed class TimerDebounceScheduler : IDebounceScheduler
{
    private readonly System.Threading.Timer _timer;
    private Action? _pendingCallback;
    private bool _disposed;

    public TimerDebounceScheduler()
    {
        _timer = new System.Threading.Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Schedule(int delayMilliseconds, Action callback)
    {
        if (_disposed)
        {
            return;
        }

        _pendingCallback = callback;
        _timer.Change(delayMilliseconds, Timeout.Infinite);
    }

    public void CancelPending()
    {
        if (_disposed)
        {
            return;
        }

        _pendingCallback = null;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pendingCallback = null;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _timer.Dispose();
    }

    private void OnTimerElapsed(object? state)
    {
        if (_disposed)
        {
            return;
        }

        var callback = _pendingCallback;
        _pendingCallback = null;
        callback?.Invoke();
    }
}
