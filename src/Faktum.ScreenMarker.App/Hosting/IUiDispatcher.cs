namespace Faktum.ScreenMarker.App.Hosting;

public interface IUiDispatcher
{
    void BeginInvoke(Action action);

    void Invoke(Action action);
}

public sealed class WpfUiDispatcher : IUiDispatcher
{
    public void BeginInvoke(Action action) =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(action);

    public void Invoke(Action action) =>
        System.Windows.Application.Current.Dispatcher.Invoke(action);
}

public sealed class SynchronousUiDispatcher : IUiDispatcher
{
    public void BeginInvoke(Action action) => action();

    public void Invoke(Action action) => action();
}
