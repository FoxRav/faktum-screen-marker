using System.Threading;
using Faktum.ScreenMarker.Platform.Windows.Diagnostics;

namespace Faktum.ScreenMarker.Platform.Windows.SingleInstance;

public sealed class SingleInstanceMutex : IDisposable
{
    private static string MutexName =>
        $@"Local\FaktumAI.ScreenMarker.SingleInstance.{Environment.UserName}";

    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;
    private bool _disposed;

    private SingleInstanceMutex(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    public static SingleInstanceMutex TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        return new SingleInstanceMutex(mutex, createdNew);
    }

    public bool IsPrimaryInstance => _ownsMutex;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                DiagnosticLog.Write("SingleInstance", "ReleaseMutex skipped.");
            }
        }

        _mutex.Dispose();
    }
}
