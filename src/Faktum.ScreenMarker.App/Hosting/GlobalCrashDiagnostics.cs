using Faktum.ScreenMarker.Platform.Windows.Diagnostics;

namespace Faktum.ScreenMarker.App.Hosting;

public static class GlobalCrashDiagnostics
{
    private static int _handlingDepth;

    public static void Install()
    {
        if (System.Windows.Application.Current is not null)
        {
            System.Windows.Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        if (IsExpectedDrawingFailure(e.Exception))
        {
            e.Handled = true;
            return;
        }

        WriteGlobal("DispatcherUnhandled", e.Exception.GetType().Name, "WpfDispatcher");
        e.Handled = false;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex && IsExpectedDrawingFailure(ex))
        {
            return;
        }

        var typeName = e.ExceptionObject is Exception exception ? exception.GetType().Name : "Unknown";
        WriteGlobal("AppDomainUnhandled", typeName, "AppDomain");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        foreach (var inner in e.Exception.Flatten().InnerExceptions)
        {
            if (IsExpectedDrawingFailure(inner))
            {
                e.SetObserved();
                return;
            }
        }

        WriteGlobal("UnobservedTask", e.Exception.GetType().Name, "TaskScheduler");
        e.SetObserved();
    }

    private static bool IsExpectedDrawingFailure(Exception exception) =>
        exception is ArgumentOutOfRangeException or InvalidOperationException;

    private static void WriteGlobal(string operation, string exceptionType, string component)
    {
        if (_handlingDepth > 0)
        {
            return;
        }

        try
        {
            _handlingDepth++;
            DiagnosticLog.Write("GlobalCrash", $"{operation}|{exceptionType}|{component}");
        }
        finally
        {
            _handlingDepth--;
        }
    }
}
