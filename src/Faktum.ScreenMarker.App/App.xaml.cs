using System.Globalization;

using System.Windows;

using Faktum.ScreenMarker.App.Hosting;

using Faktum.ScreenMarker.App;

using Faktum.ScreenMarker.Platform.Windows.Diagnostics;
using Faktum.ScreenMarker.Platform.Windows.SingleInstance;

using WpfApplication = System.Windows.Application;



namespace Faktum.ScreenMarker;



public partial class AppApplication : WpfApplication, IDisposable

{

    private SingleInstanceMutex? _singleInstance;

    private ApplicationHost? _host;



    protected override void OnStartup(StartupEventArgs e)

    {

        base.OnStartup(e);

        GlobalCrashDiagnostics.Install();

        if (e.Args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))

        {

            Shutdown(0);

            return;

        }



        if (e.Args.Contains("--platform-smoke-test", StringComparer.OrdinalIgnoreCase))

        {

            Shutdown(PlatformSmokeTestRunner.Run());

            return;

        }



        if (!IsTestHostStartup())

        {

            _singleInstance = SingleInstanceMutex.TryAcquire();

            if (!_singleInstance.IsPrimaryInstance)

            {

                Shutdown(0);

                return;

            }

        }



        if (!IsTestHostStartup())

        {

            _host = new ApplicationHost();

            _host.Start();

        }

    }



    private static bool IsTestHostStartup() =>

        string.Equals(Environment.GetEnvironmentVariable("FAKTUM_SCREENMARKER_TEST_HOST"), "1", StringComparison.Ordinal);



    protected override void OnExit(ExitEventArgs e)

    {

        _host?.Dispose();

        _singleInstance?.Dispose();

        base.OnExit(e);

    }



    internal static void ApplyCulture(string? languageOverride)

    {

        var cultureName = string.IsNullOrWhiteSpace(languageOverride)

            ? CultureInfo.CurrentUICulture.Name

            : languageOverride;

        if (!cultureName.StartsWith("fi", StringComparison.OrdinalIgnoreCase))

        {

            cultureName = "en-US";

        }



        var culture = CultureInfo.GetCultureInfo(cultureName);

        CultureInfo.CurrentCulture = culture;

        CultureInfo.CurrentUICulture = culture;

        CultureInfo.DefaultThreadCurrentCulture = culture;

        CultureInfo.DefaultThreadCurrentUICulture = culture;



        Current.Resources.MergedDictionaries.Clear();

        var dict = new ResourceDictionary
        {
            Source = new Uri(cultureName.StartsWith("fi", StringComparison.OrdinalIgnoreCase)
                ? "/FaktumScreenMarker;component/Resources/Strings.fi-FI.xaml"
                : "/FaktumScreenMarker;component/Resources/Strings.en-US.xaml", UriKind.Relative),
        };

        Current.Resources.MergedDictionaries.Add(dict);

    }



    public static string GetString(string key) =>

        Current.TryFindResource(key) as string ?? key;



    public void Dispose()

    {

        _host?.Dispose();

        _singleInstance?.Dispose();

        GC.SuppressFinalize(this);

    }

}

