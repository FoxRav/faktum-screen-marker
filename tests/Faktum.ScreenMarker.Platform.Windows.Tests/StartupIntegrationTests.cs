using Faktum.ScreenMarker.App.Hosting;
using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Settings;
using Faktum.ScreenMarker.Platform.Windows.Startup;

namespace Faktum.ScreenMarker.Platform.Windows.Tests;

internal sealed class FakeStartupRunKey : IStartupRunKey
{
    private readonly Dictionary<string, string> _values = new();

    public string? Entry => HasEntry ? _values[StartupIntegration.ValueName] : null;

    public bool HasEntry => _values.ContainsKey(StartupIntegration.ValueName);

    public int SetValueCount { get; private set; }

    public int DeleteValueCount { get; private set; }

    public string? GetValue(string name) => _values.TryGetValue(name, out var value) ? value : null;

    public void SetValue(string name, string value)
    {
        _values[name] = value;
        SetValueCount++;
    }

    public void DeleteValue(string name, bool throwOnMissingValue)
    {
        DeleteValueCount++;
        if (!throwOnMissingValue)
        {
            _values.Remove(name);
            return;
        }

        if (!_values.Remove(name))
        {
            throw new ArgumentException($"Value '{name}' not found.");
        }
    }
}

internal static class StartupTestStrings
{
    public static string Quoted(string path) => $"\"{path}\"";

    public static string QuotedCurrentExe() => Quoted(Environment.ProcessPath ?? string.Empty);
}

public class StartupIntegrationTests
{
    private readonly string _exePath = @"C:\Program Files\FaktumScreenMarker\FaktumScreenMarker.exe";

    [Fact]
    public void ApplyEnabledWritesEntryWhenMissing()
    {
        var key = new FakeStartupRunKey();
        var sut = new StartupIntegration(key);

        sut.Apply(true, _exePath);

        Assert.True(key.HasEntry);
        Assert.Equal(StartupTestStrings.Quoted(_exePath), key.Entry);
        Assert.Equal(1, key.SetValueCount);
    }

    [Fact]
    public void ApplyEnabledLeavesCorrectEntryUnchanged()
    {
        var key = new FakeStartupRunKey();
        key.SetValue(StartupIntegration.ValueName, StartupTestStrings.Quoted(_exePath));
        var writesBefore = key.SetValueCount;
        var sut = new StartupIntegration(key);

        sut.Apply(true, _exePath);

        Assert.Equal(StartupTestStrings.Quoted(_exePath), key.Entry);
        Assert.Equal(writesBefore, key.SetValueCount);
    }

    [Fact]
    public void ApplyEnabledRepairsUnquotedButCorrectPathToCanonicalValue()
    {
        var key = new FakeStartupRunKey();
        key.SetValue(StartupIntegration.ValueName, _exePath);
        var writesBefore = key.SetValueCount;
        var sut = new StartupIntegration(key);

        sut.Apply(true, _exePath);

        Assert.Equal(StartupTestStrings.Quoted(_exePath), key.Entry);
        Assert.Equal(writesBefore + 1, key.SetValueCount);
    }

    [Fact]
    public void ApplyEnabledRepairsStaleInstallationPath()
    {
        var key = new FakeStartupRunKey();
        key.SetValue(StartupIntegration.ValueName, StartupTestStrings.Quoted(@"C:\Old\Install\FaktumScreenMarker.exe"));
        var writesBefore = key.SetValueCount;
        var sut = new StartupIntegration(key);

        sut.Apply(true, _exePath);

        Assert.Equal(StartupTestStrings.Quoted(_exePath), key.Entry);
        Assert.Equal(writesBefore + 1, key.SetValueCount);
    }

    [Fact]
    public void ApplyEnabledRepairsMalformedEntry()
    {
        var key = new FakeStartupRunKey();
        key.SetValue(StartupIntegration.ValueName, "not-a-valid-path");
        var sut = new StartupIntegration(key);

        sut.Apply(true, _exePath);

        Assert.Equal(StartupTestStrings.Quoted(_exePath), key.Entry);
    }

    [Fact]
    public void ApplyEnabledDoesNotWriteWhenExecutablePathEmpty()
    {
        var key = new FakeStartupRunKey();
        var sut = new StartupIntegration(key);

        sut.Apply(true, string.Empty);

        Assert.False(key.HasEntry);
        Assert.Equal(0, key.SetValueCount);
    }

    [Fact]
    public void ApplyDisabledRemovesExistingEntry()
    {
        var key = new FakeStartupRunKey();
        key.SetValue(StartupIntegration.ValueName, StartupTestStrings.Quoted(_exePath));
        var sut = new StartupIntegration(key);

        sut.Apply(false, _exePath);

        Assert.False(key.HasEntry);
        Assert.Equal(1, key.DeleteValueCount);
    }

    [Fact]
    public void ApplyDisabledNeverReenablesAutostart()
    {
        var key = new FakeStartupRunKey();
        var sut = new StartupIntegration(key);

        sut.Apply(false, _exePath);
        sut.Apply(false, _exePath);

        Assert.False(key.HasEntry);
        Assert.Equal(0, key.SetValueCount);
    }
}

[Collection("WpfSta")]
public class ApplicationHostStartupSyncTests : IDisposable
{
    private readonly string _tempDirectory;

    public ApplicationHostStartupSyncTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        SettingsService.UseStoreForTesting(new JsonFileSettingsStore(_tempDirectory));
    }

    public void Dispose()
    {
        SettingsService.ResetStoreForTesting();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static void SeedSettings(bool startWithWindows)
    {
        var settings = AppSettings.CreateDefault();
        settings.StartWithWindows = startWithWindows;
        SettingsService.Save(settings);
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);

    [Fact]
    public void StartReconcilesRegistryToDisabledSettingsRemovesStaleRunEntry()
    {
        var key = new FakeStartupRunKey();
        key.SetValue(StartupIntegration.ValueName, StartupTestStrings.Quoted(@"C:\Stale\FakeMarker.exe"));
        var startup = new StartupIntegration(key);
        SeedSettings(false);

        RunSta(() =>
        {
            using var host = new ApplicationHost(startup);
            host.Start();
        });

        // settings.json == false is the source of truth -> stale registry entry removed.
        Assert.False(key.HasEntry);
        Assert.False(SettingsService.Load().StartWithWindows);
    }

    [Fact]
    public void StartReconcilesRegistryToEnabledSettingsRepairsStaleRunEntry()
    {
        var key = new FakeStartupRunKey();
        key.SetValue(StartupIntegration.ValueName, StartupTestStrings.Quoted(@"C:\Stale\FakeMarker.exe"));
        var startup = new StartupIntegration(key);
        SeedSettings(true);

        RunSta(() =>
        {
            using var host = new ApplicationHost(startup);
            host.Start();
        });

        Assert.Equal(StartupTestStrings.QuotedCurrentExe(), key.Entry);
        Assert.True(SettingsService.Load().StartWithWindows);
    }

    [Fact]
    public void ApplyStartWithWindowsChangeTrueKeepsSettingsAndRegistryInSync()
    {
        var key = new FakeStartupRunKey();
        var startup = new StartupIntegration(key);
        SeedSettings(false);

        RunSta(() =>
        {
            using var host = new ApplicationHost(startup);
            host.Start();
            host.ApplyStartWithWindowsChange(true);
        });

        Assert.True(SettingsService.Load().StartWithWindows);
        Assert.Equal(StartupTestStrings.QuotedCurrentExe(), key.Entry);
    }

    [Fact]
    public void ApplyStartWithWindowsChangeFalseKeepsSettingsAndRegistryInSync()
    {
        var key = new FakeStartupRunKey();
        var startup = new StartupIntegration(key);
        key.SetValue(StartupIntegration.ValueName, StartupTestStrings.QuotedCurrentExe());
        SeedSettings(true);

        RunSta(() =>
        {
            using var host = new ApplicationHost(startup);
            host.Start();
            host.ApplyStartWithWindowsChange(false);
        });

        Assert.False(SettingsService.Load().StartWithWindows);
        Assert.False(key.HasEntry);
    }

    [Fact]
    public void ApplyStartWithWindowsChangeTrueRepairsStaleRegistryPath()
    {
        var key = new FakeStartupRunKey();
        key.SetValue(StartupIntegration.ValueName, StartupTestStrings.Quoted(@"C:\Old\Install\Fake.exe"));
        var startup = new StartupIntegration(key);
        SeedSettings(false);

        RunSta(() =>
        {
            using var host = new ApplicationHost(startup);
            host.Start();
            host.ApplyStartWithWindowsChange(true);
        });

        Assert.True(SettingsService.Load().StartWithWindows);
        Assert.Equal(StartupTestStrings.QuotedCurrentExe(), key.Entry);
    }
}
