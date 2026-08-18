using Faktum.ScreenMarker.Core.Settings;

using Xunit;

namespace Faktum.ScreenMarker.Core.Tests;

public class AppSettingsDefaultsTests
{
    [Fact]
    public void CreateDefaultEnablesStartWithWindowsForNewInstallations()
    {
        var settings = AppSettings.CreateDefault();

        Assert.True(settings.StartWithWindows);
    }
}
