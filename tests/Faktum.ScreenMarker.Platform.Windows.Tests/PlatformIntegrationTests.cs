using System.Windows;
using System.Windows.Input;
using Faktum.ScreenMarker.App.Hosting;
using Faktum.ScreenMarker.App.Interaction;
using Faktum.ScreenMarker.App.Overlays;
using Faktum.ScreenMarker.App.Settings;
using Faktum.ScreenMarker.App.Toolbar;
using Faktum.ScreenMarker.Core.Application;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Keyboard;
using Faktum.ScreenMarker.Platform.Windows.Monitors;
using Faktum.ScreenMarker.Platform.Windows.Native;
using Faktum.ScreenMarker.Platform.Windows.Settings;
using Faktum.ScreenMarker.Platform.Windows.SingleInstance;
using Faktum.ScreenMarker.Platform.Windows.Windowing;

namespace Faktum.ScreenMarker.Platform.Windows.Tests;

[Collection("SettingsStore")]
public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _productionDirectory;

    public SettingsServiceTests()
    {
        _productionDirectory = SettingsPaths.SettingsDirectory;
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

    [Fact]
    public void SaveAndLoadRoundTrip()
    {
        var settings = AppSettings.CreateDefault();
        settings.PreferredStrokeWidth = 4;
        settings.PreferredColor = ColorValue.Blue;
        settings.PreferredTextFontSize = 32;
        settings.ToolbarPlacement = new ToolbarPlacement(@"\\.\DISPLAY1", 100, 50);
        SettingsService.Save(settings);

        var loaded = SettingsService.Load();
        Assert.Equal(4, loaded.PreferredStrokeWidth);
        Assert.Equal(ColorValue.Blue, loaded.PreferredColor);
        Assert.Equal(32, loaded.PreferredTextFontSize);
        Assert.Equal(100, loaded.ToolbarPlacement?.Left);
    }

    [Fact]
    public void InvalidStrokeWidthFallsBackOnLoad()
    {
        var settings = AppSettings.CreateDefault();
        settings.PreferredStrokeWidth = 999;
        SettingsService.Save(settings);
        var loaded = SettingsService.Load();
        Assert.Equal(3.0, loaded.PreferredStrokeWidth);
    }

    [Fact]
    public void LegacyChordFieldsAreIgnoredOnLoad()
    {
        var path = SettingsService.Store.SettingsFilePath;
        Directory.CreateDirectory(SettingsService.Store.SettingsDirectory);
        File.WriteAllText(path, """
            {
              "version": 1,
              "prefixKey": { "scanCode": 41, "isExtended": false },
              "keyOne": { "scanCode": 41, "isExtended": false },
              "keyTwo": { "scanCode": 3, "isExtended": false },
              "preferredStrokeWidth": 5
            }
            """);

        var loaded = SettingsService.Load();
        Assert.Equal(5, loaded.PreferredStrokeWidth);
    }

    [Fact]
    public void LegacyFallbackHotkeyFieldIsIgnoredOnLoad()
    {
        var path = SettingsService.Store.SettingsFilePath;
        Directory.CreateDirectory(SettingsService.Store.SettingsDirectory);
        File.WriteAllText(path, """
            {
              "version": 2,
              "fallbackHotkey": { "control": true, "shift": false, "alt": false, "virtualKey": 65 },
              "preferredStrokeWidth": 5
            }
            """);

        var loaded = SettingsService.Load();
        Assert.Equal(5, loaded.PreferredStrokeWidth);
    }

    [Fact]
    public void SavedSettingsDoNotContainFallbackHotkey()
    {
        var settings = AppSettings.CreateDefault();
        settings.PreferredStrokeWidth = 7;
        SettingsService.Save(settings);
        var json = File.ReadAllText(SettingsService.Store.SettingsFilePath);
        Assert.DoesNotContain("fallbackHotkey", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionSettingsPathIsNotUsedByTests()
    {
        Assert.NotEqual(_productionDirectory, SettingsService.Store.SettingsDirectory);
        Assert.False(File.Exists(Path.Combine(_productionDirectory, "settings.json.test-touch")));
    }
}

[CollectionDefinition("SingleInstance", DisableParallelization = true)]
public sealed class SingleInstanceIsolation;

[Collection("SingleInstance")]
public class SingleInstanceMutexTests
{
    [Fact]
    public void SecondInstanceDoesNotOwnMutex()
    {
        using var first = SingleInstanceMutex.TryAcquire();
        if (!first.IsPrimaryInstance)
        {
            // Published exe or another test host already holds the session mutex.
            return;
        }

        using var second = SingleInstanceMutex.TryAcquire();
        Assert.False(second.IsPrimaryInstance);
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var mutex = SingleInstanceMutex.TryAcquire();
        mutex.Dispose();
        mutex.Dispose();
    }
}

public class PhysicalActivationKeyResolverTests
{
    [Fact]
    public void ResolvesSectionKeyFromScanCode()
    {
        var native = new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 };
        var resolver = new PhysicalActivationKeyResolver(native);
        Assert.True(resolver.TryResolveVirtualKey(out var vk));
        Assert.Equal(0xC0u, vk);
        Assert.Equal(PhysicalActivationKeyResolver.SectionKeyScanCode, native.LastScanCode);
        Assert.Equal(PhysicalActivationKeyResolver.MapVirtualKeyScanCodeToVirtualKey2, native.LastMapType);
    }

    [Fact]
    public void MappingFailureReturnsFalse()
    {
        var native = new FakeHotkeyNativeApi { ResolvedVirtualKey = 0 };
        var resolver = new PhysicalActivationKeyResolver(native);
        Assert.False(resolver.TryResolveVirtualKey(out var vk));
        Assert.Equal(0u, vk);
    }

    [Fact]
    public void ResolvesUniqueKeysAcrossLayouts()
    {
        var native = new FakeHotkeyNativeApi();
        native.AddLayoutMapping(new nint(0x04090409), 0xC0);
        native.AddLayoutMapping(new nint(0x040B040B), 0xBA);
        var resolver = new PhysicalActivationKeyResolver(native);
        var keys = resolver.ResolveUniqueLayoutVirtualKeys();
        Assert.Equal(2, keys.Count);
        Assert.Contains(0xBAu, keys);
        Assert.Contains(0xC0u, keys);
    }

    [Fact]
    public void DuplicateVirtualKeysAreEliminated()
    {
        var native = new FakeHotkeyNativeApi();
        native.LayoutMappings.Add((new nint(0x04090409), 0xC0));
        native.LayoutMappings.Add((new nint(0x04090409), 0xC0));
        native.LayoutMappings.Add((new nint(0x040B040B), 0xC0));
        var resolver = new PhysicalActivationKeyResolver(native);
        Assert.Single(resolver.ResolveUniqueLayoutVirtualKeys());
    }
}

public class HotkeyRegistrationServiceTests
{
    [Fact]
    public void RegisterPrimaryIncludesModNorepeat()
    {
        var native = new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 };
        using var service = CreateService(native);
        service.AttachWindow(new nint(100));
        Assert.True(service.TryRegisterPrimary().AnyRegistered);
        Assert.Equal(HotkeyRegistrationService.ModControl | HotkeyRegistrationService.ModNorepeat, native.LastModifiers);
        Assert.Equal(HotkeyRegistrationService.PrimaryHotkeyIdStart, native.LastHotkeyId);
    }

    [Fact]
    public void PrimaryRegistrationFailureDoesNotMarkRegistered()
    {
        var native = new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0, RegisterHotKeyResult = false };
        using var service = CreateService(native);
        service.AttachWindow(new nint(100));
        Assert.False(service.TryRegisterPrimary().AnyRegistered);
        Assert.False(service.PrimaryRegistered);
    }

    [Fact]
    public void FallbackRegistrationUsesFixedMvpHotkey()
    {
        var native = new FakeHotkeyNativeApi();
        using var service = CreateService(native);
        service.AttachWindow(new nint(100));
        Assert.True(service.TryRegisterFallback());
        Assert.Equal(HotkeyRegistrationService.FallbackHotkeyId, native.LastHotkeyId);
        Assert.Equal(0x7Bu, native.LastVirtualKey);
        Assert.Equal(HotkeyRegistrationService.ModControl | HotkeyRegistrationService.ModShift | HotkeyRegistrationService.ModNorepeat, native.LastModifiers);
    }

    [Fact]
    public void LegacySettingsCannotReplaceFallbackRegistration()
    {
        var native = new FakeHotkeyNativeApi();
        using var service = CreateService(native);
        service.AttachWindow(new nint(100));
        var customHotkey = new ModifierHotkey(Control: true, Shift: false, Alt: false, VirtualKey: 0x41);
        service.TryRegisterFallback(customHotkey);
        Assert.Equal(0x41u, native.LastVirtualKey);

        service.TryRegisterFallback();
        Assert.Equal(0x7Bu, native.LastVirtualKey);
    }

    [Fact]
    public void ProcessWindowMessageAcceptsKnownHotkeyIdsOnly()
    {
        using var service = new HotkeyRegistrationService(new FakeHotkeyNativeApi(), new PhysicalActivationKeyResolver(new FakeHotkeyNativeApi()));
        Assert.True(HotkeyRegistrationService.ProcessWindowMessage(NativeMethods.WmHotkey, new nint(HotkeyRegistrationService.PrimaryHotkeyIdStart), out var primaryId));
        Assert.Equal(HotkeyRegistrationService.PrimaryHotkeyIdStart, primaryId);
        Assert.True(HotkeyRegistrationService.ProcessWindowMessage(NativeMethods.WmHotkey, new nint(HotkeyRegistrationService.PrimaryHotkeyIdStart + 1), out _));
        Assert.True(HotkeyRegistrationService.ProcessWindowMessage(NativeMethods.WmHotkey, new nint(HotkeyRegistrationService.FallbackHotkeyId), out _));
        Assert.False(HotkeyRegistrationService.ProcessWindowMessage(NativeMethods.WmHotkey, new nint(99), out _));
        Assert.False(HotkeyRegistrationService.ProcessWindowMessage(NativeMethods.WmInputlangchange, new nint(1), out _));
    }

    [Fact]
    public void RegistersMultiplePrimaryHotkeyIdsForUniqueLayouts()
    {
        var native = new FakeHotkeyNativeApi();
        native.AddLayoutMapping(new nint(0x04090409), 0xC0);
        native.AddLayoutMapping(new nint(0x040B040B), 0xBA);
        using var service = CreateService(native);
        service.AttachWindow(new nint(100));
        var result = service.TryRegisterPrimary();
        Assert.True(result.AnyRegistered);
        Assert.True(result.AllRegistered);
        Assert.Equal(2, result.RegisteredCount);
        Assert.Equal(2, service.RegisteredPrimaryVirtualKeys.Count);
        Assert.Equal(HotkeyRegistrationService.PrimaryHotkeyIdStart, native.RegisteredHotkeyIds[0]);
        Assert.Equal(HotkeyRegistrationService.PrimaryHotkeyIdStart + 1, native.RegisteredHotkeyIds[1]);
    }

    [Fact]
    public void PartialPrimaryRegistrationReportsFailureCount()
    {
        var native = new FakeHotkeyNativeApi();
        native.AddLayoutMapping(new nint(0x04090409), 0xC0);
        native.AddLayoutMapping(new nint(0x040B040B), 0xBA);
        native.SetRegisterResult(HotkeyRegistrationService.PrimaryHotkeyIdStart, true);
        native.SetRegisterResult(HotkeyRegistrationService.PrimaryHotkeyIdStart + 1, false);
        using var service = CreateService(native);
        service.AttachWindow(new nint(100));
        var result = service.TryRegisterPrimary();
        Assert.True(result.AnyRegistered);
        Assert.False(result.AllRegistered);
        Assert.Equal(1, result.RegisteredCount);
        Assert.Equal(1, result.FailedCount);
    }

    [Fact]
    public void UnregisterAllClearsRegistrationState()
    {
        var native = new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 };
        using var service = CreateService(native);
        service.AttachWindow(new nint(100));
        service.TryRegisterPrimary();
        service.TryRegisterFallback(ModifierHotkey.Default);
        service.UnregisterAll();
        Assert.False(service.PrimaryRegistered);
        Assert.False(service.FallbackRegistered);
        Assert.True(native.UnregisterCount >= 1 + HotkeyRegistrationService.MaxPrimaryHotkeySlots);
    }

    [Fact]
    public void LayoutChangeReregistrationUsesFreshVirtualKey()
    {
        var native = new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 };
        using var service = CreateService(native);
        service.AttachWindow(new nint(100));
        service.TryRegisterPrimary();
        native.ResolvedVirtualKey = 0xBA;
        Assert.True(service.TryReregisterPrimaryAfterLayoutChange().AnyRegistered);
        Assert.Equal(0xBAu, service.PrimaryVirtualKey);
        Assert.True(native.UnregisterCount >= 1);
    }

    private static HotkeyRegistrationService CreateService(FakeHotkeyNativeApi native)
    {
        var resolver = new PhysicalActivationKeyResolver(native);
        return new HotkeyRegistrationService(native, resolver);
    }
}

public class HostInitializationCoordinatorTests
{
    [Fact]
    public void SubscribeBeforeEnsureHandleOrdering()
    {
        var messageWindow = new FakeHiddenMessageWindowHost();
        using var hotkeys = new HotkeyRegistrationService(new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 }, new PhysicalActivationKeyResolver(new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 }));
        using var coordinator = new HostInitializationCoordinator(messageWindow, hotkeys);
        coordinator.Initialize(() => messageWindow.EnsureHandleCreated());
        Assert.Equal("SubscribeEvents", coordinator.CallLog[0]);
        Assert.Equal("EnsureHiddenWindowHandle", coordinator.CallLog[1]);
    }

    [Fact]
    public void PrimaryRegistrationRequiresValidHandle()
    {
        var messageWindow = new FakeHiddenMessageWindowHost { HasValidHandleValue = false };
        using var hotkeys = new HotkeyRegistrationService(new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 }, new PhysicalActivationKeyResolver(new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 }));
        using var coordinator = new HostInitializationCoordinator(messageWindow, hotkeys);
        Assert.False(coordinator.TryRegisterPrimaryHotkey());
    }

    [Fact]
    public void OneHotkeyMessageProducesOneToggleRequest()
    {
        var messageWindow = new FakeHiddenMessageWindowHost();
        using var hotkeys = new HotkeyRegistrationService(new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 }, new PhysicalActivationKeyResolver(new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 }));
        using var coordinator = new HostInitializationCoordinator(messageWindow, hotkeys);
        var toggleCount = 0;
        coordinator.HotkeyToggleRequested += () => toggleCount++;
        coordinator.Initialize(() => messageWindow.EnsureHandleCreated());
        hotkeys.AttachWindow(messageWindow.Handle);
        messageWindow.RaiseHotkeyPressed(HotkeyRegistrationService.PrimaryHotkeyIdStart);
        Assert.Equal(1, toggleCount);
        messageWindow.RaiseHotkeyPressed(HotkeyRegistrationService.FallbackHotkeyId);
        Assert.Equal(2, toggleCount);
    }

    [Fact]
    public void UnrelatedHotkeyIdProducesZeroToggles()
    {
        var messageWindow = new FakeHiddenMessageWindowHost();
        using var hotkeys = new HotkeyRegistrationService(new FakeHotkeyNativeApi(), new PhysicalActivationKeyResolver(new FakeHotkeyNativeApi()));
        using var coordinator = new HostInitializationCoordinator(messageWindow, hotkeys);
        var toggleCount = 0;
        coordinator.HotkeyToggleRequested += () => toggleCount++;
        coordinator.Initialize(() => messageWindow.EnsureHandleCreated());
        messageWindow.RaiseHotkeyPressed(99);
        Assert.Equal(0, toggleCount);
    }

    [Fact]
    public void DisposeUnregistersHotkeysOnce()
    {
        var native = new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 };
        var messageWindow = new FakeHiddenMessageWindowHost();
        using var hotkeys = new HotkeyRegistrationService(native, new PhysicalActivationKeyResolver(native));
        var coordinator = new HostInitializationCoordinator(messageWindow, hotkeys);
        coordinator.Initialize(() => messageWindow.EnsureHandleCreated());
        hotkeys.AttachWindow(messageWindow.Handle);
        hotkeys.TryRegisterPrimary();
        hotkeys.TryRegisterFallback();
        coordinator.Dispose();
        Assert.Contains("UnregisterHotkeys", coordinator.CallLog);
        Assert.True(native.UnregisterCount >= 2);
    }
}

internal sealed class FakeHiddenMessageWindowHost : IHiddenMessageWindowHost
{
    public nint Handle { get; private set; } = new(123);

    public bool HasValidHandleValue { get; set; } = true;

    public bool HasValidHandle => HasValidHandleValue && Handle != 0;

    public event Action? HandleReady;

    public event Action<int>? HotkeyPressed;

    public event Action? InputLanguageChanged;

    public void EnsureHandleCreated() => HandleReady?.Invoke();

    public void RaiseHotkeyPressed(int hotkeyId) => HotkeyPressed?.Invoke(hotkeyId);

    public void RaiseInputLanguageChanged() => InputLanguageChanged?.Invoke();
}

internal sealed class FakeHotkeyNativeApi : IHotkeyNativeApi
{
    public nint KeyboardLayout { get; set; } = new(0x04090409);

    public uint ResolvedVirtualKey { get; set; } = 0xC0;

    public Dictionary<nint, uint> LayoutVirtualKeys { get; } = [];

    public void AddLayoutMapping(nint layout, uint virtualKey) => LayoutVirtualKeys[layout] = virtualKey;

    public void SetRegisterResult(int hotkeyId, bool result) => RegisterHotKeyResults[hotkeyId] = result;

    public List<(nint Layout, uint VirtualKey)> LayoutMappings { get; } = [];

    public Dictionary<int, bool> RegisterHotKeyResults { get; } = [];

    public bool RegisterHotKeyResult { get; set; } = true;

    public ushort LastScanCode { get; private set; }

    public uint LastMapType { get; private set; }

    public nint LastKeyboardLayout { get; private set; }

    public int LastHotkeyId { get; private set; }

    public List<int> RegisteredHotkeyIds { get; } = [];

    public uint LastModifiers { get; private set; }

    public uint LastVirtualKey { get; private set; }

    public int UnregisterCount { get; private set; }

    public nint GetKeyboardLayout(uint threadId)
    {
        _ = threadId;
        return KeyboardLayout;
    }

    public IReadOnlyList<nint> GetKeyboardLayoutList()
    {
        if (LayoutMappings.Count > 0)
        {
            return LayoutMappings.Select(static mapping => mapping.Layout).Distinct().ToArray();
        }

        if (LayoutVirtualKeys.Count > 0)
        {
            return LayoutVirtualKeys.Keys.ToArray();
        }

        return KeyboardLayout == 0 ? [] : [KeyboardLayout];
    }

    public uint MapVirtualKey2(uint scanCode, uint mapType, nint keyboardLayout)
    {
        LastScanCode = (ushort)scanCode;
        LastMapType = mapType;
        LastKeyboardLayout = keyboardLayout;
        foreach (var mapping in LayoutMappings)
        {
            if (mapping.Layout == keyboardLayout)
            {
                return mapping.VirtualKey;
            }
        }

        if (LayoutVirtualKeys.TryGetValue(keyboardLayout, out var mapped))
        {
            return mapped;
        }

        return ResolvedVirtualKey;
    }

    public bool RegisterHotKey(nint windowHandle, int hotkeyId, uint modifiers, uint virtualKey)
    {
        _ = windowHandle;
        LastHotkeyId = hotkeyId;
        LastModifiers = modifiers;
        LastVirtualKey = virtualKey;
        RegisteredHotkeyIds.Add(hotkeyId);
        if (RegisterHotKeyResults.TryGetValue(hotkeyId, out var result))
        {
            return result;
        }

        return RegisterHotKeyResult;
    }

    public bool UnregisterHotKey(nint windowHandle, int hotkeyId)
    {
        _ = windowHandle;
        _ = hotkeyId;
        UnregisterCount++;
        return true;
    }
}

internal sealed class FakeDebounceScheduler : IDebounceScheduler
{
    private Action? _callback;
    private bool _disposed;

    public int PendingScheduleCount { get; private set; }

    public void Schedule(int delayMilliseconds, Action callback)
    {
        if (_disposed)
        {
            return;
        }

        _ = delayMilliseconds;
        _callback = callback;
        PendingScheduleCount++;
    }

    public void CancelPending()
    {
        _callback = null;
        PendingScheduleCount = 0;
    }

    public void RaisePending()
    {
        var callback = _callback;
        _callback = null;
        callback?.Invoke();
    }

    public void Dispose()
    {
        _disposed = true;
        _callback = null;
        PendingScheduleCount = 0;
    }
}

public class DisplayTopologyCoordinatorTests
{
    [Fact]
    public void RebuildRunsOnUiDispatcher()
    {
        var dispatcher = new RecordingUiDispatcher();
        var debounce = new FakeDebounceScheduler();
        var overlayCoordinator = new OverlayCoordinator();
        var notifications = 0;
        using var coordinator = new DisplayTopologyCoordinator(
            overlayCoordinator,
            dispatcher,
            debounce,
            () => notifications++);

        using var session = new DrawingSession();
        coordinator.SetActive(true, session, AppSettings.CreateDefault(), () => { }, null);
        coordinator.MarkInitialized();
        coordinator.TestRaiseDisplaySettingsChanged();
        debounce.RaisePending();

        Assert.Contains("BeginInvoke", dispatcher.Actions);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void DeactivateResetsInitializedAndClearsPendingRebuild()
    {
        var dispatcher = new RecordingUiDispatcher();
        var debounce = new FakeDebounceScheduler();
        using var coordinator = new DisplayTopologyCoordinator(
            new OverlayCoordinator(),
            dispatcher,
            debounce,
            () => { });

        using var session = new DrawingSession();
        coordinator.SetActive(true, session, AppSettings.CreateDefault(), () => { }, null);
        coordinator.MarkInitialized();
        debounce.Schedule(250, () => { });
        coordinator.SetActive(false, null, null, null, null);
        coordinator.TestScheduleRebuild();

        Assert.Empty(dispatcher.Actions);
        Assert.Equal(0, debounce.PendingScheduleCount);
    }

    [Fact]
    public void DisplayEventBeforeInitializationDoesNotScheduleRebuild()
    {
        var dispatcher = new RecordingUiDispatcher();
        var debounce = new FakeDebounceScheduler();
        using var coordinator = new DisplayTopologyCoordinator(
            new OverlayCoordinator(),
            dispatcher,
            debounce,
            () => { });

        using var session = new DrawingSession();
        coordinator.SetActive(true, session, AppSettings.CreateDefault(), () => { }, null);
        debounce.RaisePending();
        coordinator.TestScheduleRebuild();

        Assert.Empty(dispatcher.Actions);
    }

    [Fact]
    public void DisposeCancelsPendingDebounce()
    {
        var debounce = new FakeDebounceScheduler();
        using var coordinator = new DisplayTopologyCoordinator(
            new OverlayCoordinator(),
            new RecordingUiDispatcher(),
            debounce,
            () => { });

        using var session = new DrawingSession();
        coordinator.SetActive(true, session, AppSettings.CreateDefault(), () => { }, null);
        coordinator.MarkInitialized();
        coordinator.TestRaiseDisplaySettingsChanged();
        coordinator.Dispose();
        debounce.RaisePending();
        Assert.Equal(0, debounce.PendingScheduleCount);
    }
}

public class HostOrchestrationPathTests
{
    [Fact]
    public void HotkeyPathDrivesLifecycleOrchestrator()
    {
        var activations = 0;
        var deactivations = 0;
        var state = new ApplicationStateCoordinator();
        var orchestrator = new DrawingLifecycleOrchestrator(
            state,
            () => activations++,
            () => deactivations++);

        var messageWindow = new FakeHiddenMessageWindowHost();
        using var hotkeys = new HotkeyRegistrationService(new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 }, new PhysicalActivationKeyResolver(new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 }));
        using var hostInit = new HostInitializationCoordinator(messageWindow, hotkeys);
        hostInit.HotkeyToggleRequested += orchestrator.RequestHotkeyToggle;

        orchestrator.MarkStarted();
        hostInit.Initialize(() => messageWindow.EnsureHandleCreated());
        messageWindow.RaiseHotkeyPressed(HotkeyRegistrationService.PrimaryHotkeyIdStart);
        Assert.Equal(1, activations);
        orchestrator.NotifyActivationSucceeded();
        messageWindow.RaiseHotkeyPressed(HotkeyRegistrationService.FallbackHotkeyId);
        Assert.Equal(1, deactivations);
    }

    [Fact]
    public void ExplicitTrayPathIsIdempotentWhileActive()
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
}

public class HotkeyFailureNotificationTests
{
    [Fact]
    public void PrimaryRegistrationFailureNotifiesOnceThroughCoordinator()
    {
        var messageWindow = new FakeHiddenMessageWindowHost();
        using var hotkeys = new HotkeyRegistrationService(
            new FakeHotkeyNativeApi { ResolvedVirtualKey = 0 },
            new PhysicalActivationKeyResolver(new FakeHotkeyNativeApi { ResolvedVirtualKey = 0 }));
        using var coordinator = new HostInitializationCoordinator(messageWindow, hotkeys);
        var notifications = 0;
        coordinator.Initialize(() => messageWindow.EnsureHandleCreated());
        hotkeys.AttachWindow(messageWindow.Handle);
        if (!coordinator.TryRegisterPrimaryHotkey())
        {
            notifications++;
        }

        Assert.Equal(1, notifications);
        Assert.True(coordinator.TryRegisterFallbackHotkey());
    }

    [Fact]
    public void LayoutChangeReregistrationFailureNotifiesOnce()
    {
        var native = new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 };
        var messageWindow = new FakeHiddenMessageWindowHost();
        using var hotkeys = new HotkeyRegistrationService(native, new PhysicalActivationKeyResolver(native));
        using var coordinator = new HostInitializationCoordinator(messageWindow, hotkeys);
        var notifications = 0;
        coordinator.PrimaryHotkeyReregistrationFailed += () => notifications++;
        coordinator.Initialize(() => messageWindow.EnsureHandleCreated());
        hotkeys.AttachWindow(messageWindow.Handle);
        hotkeys.TryRegisterPrimary();
        hotkeys.TryRegisterFallback();

        native.ResolvedVirtualKey = 0;
        native.LayoutVirtualKeys.Clear();
        messageWindow.RaiseInputLanguageChanged();

        Assert.Equal(1, notifications);
        Assert.True(hotkeys.FallbackRegistered);
    }

    [Fact]
    public void InputLanguageChangedTriggersSingleReregistrationCycle()
    {
        var native = new FakeHotkeyNativeApi { ResolvedVirtualKey = 0xC0 };
        var messageWindow = new FakeHiddenMessageWindowHost();
        using var hotkeys = new HotkeyRegistrationService(native, new PhysicalActivationKeyResolver(native));
        using var coordinator = new HostInitializationCoordinator(messageWindow, hotkeys);
        coordinator.Initialize(() => messageWindow.EnsureHandleCreated());
        hotkeys.AttachWindow(messageWindow.Handle);
        hotkeys.TryRegisterPrimary();

        native.ResolvedVirtualKey = 0xBA;
        messageWindow.RaiseInputLanguageChanged();

        Assert.Contains("ReregisterPrimaryHotkey", coordinator.CallLog);
        Assert.Equal(0xBAu, hotkeys.PrimaryVirtualKey);
    }
}

public class ApplicationStateAfterDisplayRebuildTests
{
    [Fact]
    public void ActivationHotkeyStillDeactivatesAfterDisplayRebuildFailure()
    {
        var coordinator = new ApplicationStateCoordinator();
        coordinator.MarkStarted();
        coordinator.RequestHotkeyToggle();
        coordinator.MarkActivationSucceeded();
        Assert.Equal(ApplicationState.Active, coordinator.State);

        var failed = coordinator.MarkDisplayRebuildFailed();
        Assert.Equal(ApplicationState.FaultedRecoverable, failed.NewState);

        coordinator.RequestHotkeyToggle();
        var activating = coordinator.MarkActivationSucceeded();
        Assert.Equal(ApplicationState.Active, activating.NewState);

        var deactivate = coordinator.RequestHotkeyToggle();
        Assert.Equal(ApplicationState.Deactivating, deactivate.NewState);
    }
}

internal sealed class RecordingUiDispatcher : IUiDispatcher
{
    public List<string> Actions { get; } = [];

    public void BeginInvoke(Action action)
    {
        Actions.Add("BeginInvoke");
        action();
    }

    public void Invoke(Action action)
    {
        Actions.Add("Invoke");
        action();
    }
}

[Collection("SettingsStore")]
public class SettingsPersistenceCoordinatorTests
{
    [Fact]
    public void FlushPersistsToolbarPlacement()
    {
        var temp = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        SettingsService.UseStoreForTesting(new JsonFileSettingsStore(temp));
        try
        {
            var settings = AppSettings.CreateDefault();
            using (var coordinator = new SettingsPersistenceCoordinator(settings))
            {
                coordinator.UpdateToolbarPlacement(new ToolbarPlacement(@"\\.\DISPLAY1", 10, 20));
                coordinator.Flush();
            }

            Assert.True(File.Exists(SettingsService.Store.SettingsFilePath));
            var loaded = SettingsService.Load();
            Assert.NotNull(loaded.ToolbarPlacement);
            Assert.Equal(10, loaded.ToolbarPlacement?.Left);
        }
        finally
        {
            SettingsService.ResetStoreForTesting();
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    [Fact]
    public void ApplySettingsAndColorChangePersistAfterFlushAndReload()
    {
        var temp = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        SettingsService.UseStoreForTesting(new JsonFileSettingsStore(temp));
        try
        {
            var settings = AppSettings.CreateDefault();
            using var coordinator = new SettingsPersistenceCoordinator(settings);
            coordinator.ApplySettings(new AppSettings { PreferredColor = ColorValue.Green, LanguageOverride = "fi-FI" });
            coordinator.UpdatePreferredStrokeWidth(6);
            coordinator.Flush();

            var loaded = SettingsService.Load();
            Assert.Equal(ColorValue.Green, loaded.PreferredColor);
            Assert.Equal(6, loaded.PreferredStrokeWidth);
            Assert.Equal("fi-FI", loaded.LanguageOverride);
        }
        finally
        {
            SettingsService.ResetStoreForTesting();
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }

    [Fact]
    public void SaveFailureNotifiesOnce()
    {
        var settings = AppSettings.CreateDefault();
        var notifications = 0;
        SettingsService.UseStoreForTesting(new ThrowingSettingsStore());
        try
        {
            using var coordinator = new SettingsPersistenceCoordinator(
                settings,
                (_, _) => notifications++);
            coordinator.UpdatePreferredColor(ColorValue.Red);
            coordinator.Flush();
            coordinator.Flush();
            Assert.Equal(1, notifications);
        }
        finally
        {
            SettingsService.ResetStoreForTesting();
        }
    }

    [Fact]
    public void DirtyDisposePerformsExactlyOneSave()
    {
        var settings = AppSettings.CreateDefault();
        var saves = 0;
        SettingsService.UseStoreForTesting(new CountingSettingsStore(() => saves++));
        try
        {
            var coordinator = new SettingsPersistenceCoordinator(settings);
            coordinator.UpdatePreferredStrokeWidth(6);
            coordinator.Dispose();
            Assert.Equal(1, saves);
        }
        finally
        {
            SettingsService.ResetStoreForTesting();
        }
    }

    [Fact]
    public void CleanDisposePerformsZeroSaves()
    {
        var settings = AppSettings.CreateDefault();
        var saves = 0;
        SettingsService.UseStoreForTesting(new CountingSettingsStore(() => saves++));
        try
        {
            var coordinator = new SettingsPersistenceCoordinator(settings);
            coordinator.Dispose();
            Assert.Equal(0, saves);
        }
        finally
        {
            SettingsService.ResetStoreForTesting();
        }
    }

    [Fact]
    public void FailedFinalSaveOnDisposeNotifiesOnce()
    {
        var settings = AppSettings.CreateDefault();
        var notifications = 0;
        SettingsService.UseStoreForTesting(new ThrowingSettingsStore());
        try
        {
            var coordinator = new SettingsPersistenceCoordinator(settings, (_, _) => notifications++);
            coordinator.UpdatePreferredColor(ColorValue.Red);
            coordinator.Dispose();
            Assert.Equal(1, notifications);
        }
        finally
        {
            SettingsService.ResetStoreForTesting();
        }
    }

    [Fact]
    public void NoSaveAfterDisposal()
    {
        var settings = AppSettings.CreateDefault();
        var saves = 0;
        SettingsService.UseStoreForTesting(new CountingSettingsStore(() => saves++));
        try
        {
            var coordinator = new SettingsPersistenceCoordinator(settings);
            coordinator.UpdatePreferredStrokeWidth(6);
            coordinator.Dispose();
            var savesAfterDispose = saves;
            coordinator.UpdatePreferredStrokeWidth(8);
            coordinator.Flush();
            Assert.Equal(savesAfterDispose, saves);
            Assert.Equal(1, savesAfterDispose);
        }
        finally
        {
            SettingsService.ResetStoreForTesting();
        }
    }
}

internal sealed class ThrowingSettingsStore : ISettingsStore
{
    public string SettingsDirectory => Path.GetTempPath();

    public string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load() => AppSettings.CreateDefault();

    public void Save(AppSettings settings) => throw new IOException("save failed");
}

internal sealed class CountingSettingsStore(Action onSave) : ISettingsStore
{
    public string SettingsDirectory => Path.GetTempPath();

    public string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load() => AppSettings.CreateDefault();

    public void Save(AppSettings settings) => onSave();
}

public class ToolbarPlacementHelperTests
{
    [Fact]
    public void ResolveInitialPlacementUsesMonitorVirtualOrigin()
    {
        var monitor = new Faktum.ScreenMarker.Platform.Windows.Monitors.MonitorInfo(
            @"\\.\DISPLAY2", 1920, 0, 1920, 1080, 120, 120);
        var settings = AppSettings.CreateDefault();
        var (left, top) = Faktum.ScreenMarker.App.Toolbar.ToolbarPlacementHelper.ResolveInitialPlacement(settings, monitor);
        Assert.True(left >= 1920 / monitor.DipScaleX);
        Assert.True(top >= 0);
    }
}

[CollectionDefinition("SettingsStore", DisableParallelization = true)]
public sealed class SettingsStoreIsolation;

public class DrawingRendererDpiTests
{
    [Fact]
    public void TextRenderingUsesProvidedPixelsPerDip()
    {
        var renderer = new Faktum.ScreenMarker.App.Drawing.DrawingRenderer();
        var text = new Faktum.ScreenMarker.Core.Drawing.TextAnnotation(
            1,
            @"\\.\DISPLAY1",
            Faktum.ScreenMarker.Core.Drawing.StrokeStyle.DefaultPen,
            new Faktum.ScreenMarker.Core.Drawing.Point2D(0, 0),
            "Hi",
            24);
        renderer.Render([text], null, pixelsPerDip: 1.5);
        Assert.NotNull(renderer.CommittedVisual);
        Assert.Equal(1.5, renderer.LastRenderedPixelsPerDip);
    }
}

[CollectionDefinition("WpfSta", DisableParallelization = true)]
public sealed class WpfStaIsolation;

internal static class StaTestHost
{
    private static readonly object Gate = new();
    private static Thread? _thread;
    private static AutoResetEvent? _ready;
    private static System.Windows.Application? _application;

    public static void Run(Action action)
    {
        EnsureThreadStarted();
        Exception? captured = null;
        _application!.Dispatcher.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        if (captured is not null)
        {
            throw captured;
        }
    }

    private static void EnsureThreadStarted()
    {
        lock (Gate)
        {
            if (_thread is not null)
            {
                return;
            }

            _ready = new AutoResetEvent(false);
            _thread = new Thread(() =>
            {
                Environment.SetEnvironmentVariable("FAKTUM_SCREENMARKER_TEST_HOST", "1");
                if (System.Windows.Application.Current is null)
                {
                    _ = new Faktum.ScreenMarker.AppApplication
                    {
                        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
                    };
                }

                _application = System.Windows.Application.Current;
                _ready!.Set();
                System.Windows.Threading.Dispatcher.Run();
            })
            {
                IsBackground = true,
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        _ready!.WaitOne();
        if (_application is null)
        {
            throw new InvalidOperationException("WPF test application failed to initialize on STA thread.");
        }
    }
}

[Collection("WpfSta")]
public class HiddenMessageWindowIntegrationTests
{
    [Fact]
    public void RepeatedEnsureHandleCreatesSingleWndProcHook()
    {
        RunSta(() =>
        {
            using var hotkeys = new HotkeyRegistrationService();
            var window = new HiddenMessageWindow(hotkeys);
            window.EnsureHandleCreated();
            Assert.True(window.IsWndProcHookAttached);
            window.EnsureHandleCreated();
            Assert.True(window.IsWndProcHookAttached);
            window.Close();
        });
    }

    [Fact]
    public void SyntheticHotkeyMessageFiresOnce()
    {
        RunSta(() =>
        {
            using var hotkeys = new HotkeyRegistrationService();
            var window = new HiddenMessageWindow(hotkeys);
            window.EnsureHandleCreated();
            hotkeys.AttachWindow(window.Handle);
            hotkeys.TryRegisterPrimary();

            var events = 0;
            window.HotkeyPressed += _ => events++;
            HotkeyWindowMessaging.PostHotkeyMessage(window.Handle, HotkeyRegistrationService.PrimaryHotkeyIdStart);
            WpfTestContext.Pump();

            Assert.Equal(1, events);
            window.Close();
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

internal static class WpfTestContext
{
    public static void EnsureApplication()
    {
        if (System.Windows.Application.Current is not null)
        {
            return;
        }

        throw new InvalidOperationException(
            "WPF Application is not available. STA test host must initialize the application before running UI tests.");
    }

    public static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}

[Collection("WpfSta")]
public class OverlayRebuildOrderingTests
{
    [Fact]
    public void RebuildDetachesToolbarBeforeClosingOverlays()
    {
        RunSta(() =>
        {
            var coordinator = new OverlayCoordinator();
            using var session = new DrawingSession();
            var settings = AppSettings.CreateDefault();
            var temp = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            SettingsService.UseStoreForTesting(new JsonFileSettingsStore(temp));
            try
            {
                using var persistence = new SettingsPersistenceCoordinator(settings);
                Assert.True(coordinator.Activate(session, settings, persistence, () => { }));
                coordinator.RebuildOverlays(session, settings, () => { });
                var log = coordinator.RebuildCallLog;
                Assert.Equal("PreserveToolbarState", log[0]);
                Assert.Equal("FlushToolbarSettingsOnce", log[1]);
                Assert.Equal("DetachToolbarFromOverlay", log[2]);
                Assert.Equal("CloseOldOverlays", log[3]);
                Assert.Contains("ReattachExistingToolbar", log);
                Assert.Equal("VerifyToolbarHitTesting", log[^1]);
            }
            finally
            {
                coordinator.Deactivate();
                SettingsService.ResetStoreForTesting();
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

[Collection("WpfSta")]
public class ApplicationHostShutdownTests
{
    [Fact]
    public void RepeatedStopAndDisposeAreIdempotent()
    {
        RunSta(() =>
        {
            var temp = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            SettingsService.UseStoreForTesting(new JsonFileSettingsStore(temp));
            try
            {
                using var host = new ApplicationHost();
                host.Start();
                host.Stop();
                host.Stop();
                host.Dispose();
                host.Dispose();
                Assert.Equal(ApplicationState.Stopping, host.State);
            }
            finally
            {
                SettingsService.ResetStoreForTesting();
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

public class OverlayPointerInputControllerTests
{
    private const string MonitorA = @"\\.\DISPLAY1";

    [Fact]
    public void PenDragOnEmptyOverlayCommitsStroke()
    {
        using var session = new DrawingSession { ActiveTool = DrawingTool.Pen };
        var controller = new OverlayPointerInputController(session, MonitorA);
        controller.BeginLeftButtonDrag(new Point2D(1, 1), DrawingTool.Pen, _ => { });
        controller.UpdateDrag(new Point2D(5, 5), shift: false);
        Assert.True(controller.CompleteDrag(session));
        Assert.Single(session.Objects);
        Assert.False(controller.IsDrawing);
        Assert.Equal(0, controller.LivePointCount);
        Assert.Null(session.PreviewObject);
    }

    [Fact]
    public void RectangleDragOnEmptyOverlayCommitsShape()
    {
        using var session = new DrawingSession { ActiveTool = DrawingTool.Rectangle };
        var controller = new OverlayPointerInputController(session, MonitorA);
        controller.BeginLeftButtonDrag(new Point2D(2, 2), DrawingTool.Rectangle, _ => { });
        controller.UpdateDrag(new Point2D(20, 20), shift: false);
        Assert.True(controller.CompleteDrag(session));
        Assert.Single(session.Objects);
        Assert.IsType<RectangleAnnotation>(session.Objects[0]);
    }

    [Fact]
    public void EraserClickReachesSession()
    {
        using var session = new DrawingSession { ActiveTool = DrawingTool.Eraser };
        session.BeginPreview(MonitorA, new LineAnnotation(session.AllocateId(), MonitorA, StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(10, 10)));
        session.CommitPreview();
        var controller = new OverlayPointerInputController(session, MonitorA);
        controller.BeginLeftButtonDrag(new Point2D(5, 0), DrawingTool.Eraser, _ => { });
        Assert.Empty(session.Objects);
    }

    [Fact]
    public void InteractionToolLatchesDuringDragDespiteSessionToolChange()
    {
        using var session = new DrawingSession { ActiveTool = DrawingTool.Pen };
        var controller = new OverlayPointerInputController(session, MonitorA);
        controller.BeginLeftButtonDrag(new Point2D(1, 1), DrawingTool.Pen, _ => { });
        session.ActiveTool = DrawingTool.Line;
        controller.UpdateDrag(new Point2D(10, 10), shift: false);
        Assert.Equal(DrawingTool.Pen, controller.LatchedInteractionTool);
        Assert.True(controller.CompleteDrag(session));
        Assert.IsType<FreehandStroke>(session.Objects[0]);
        Assert.Null(controller.LatchedInteractionTool);
    }

    [Fact]
    public void CancelClearsLatchedInteractionTool()
    {
        using var session = new DrawingSession { ActiveTool = DrawingTool.Rectangle };
        var controller = new OverlayPointerInputController(session, MonitorA);
        controller.BeginLeftButtonDrag(new Point2D(1, 1), DrawingTool.Rectangle, _ => { });
        controller.CancelDrawing(session);
        Assert.Null(controller.LatchedInteractionTool);
        Assert.False(controller.IsDrawing);
    }

    [Fact]
    public void TwoSequentialPenStrokesKeepIndependentControllerState()
    {
        using var session = new DrawingSession { ActiveTool = DrawingTool.Pen };
        var controller = new OverlayPointerInputController(session, MonitorA);

        controller.BeginLeftButtonDrag(new Point2D(1, 1), DrawingTool.Pen, _ => { });
        for (var i = 2; i <= 50; i++)
        {
            controller.UpdateDrag(new Point2D(i, Math.Sin(i) * 10), shift: false);
        }

        Assert.True(controller.CompleteDrag(session));
        var firstId = session.Objects[0].Id;
        Assert.False(controller.IsDrawing);
        Assert.Null(session.PreviewObject);
        Assert.Equal(0, controller.LivePointCount);

        controller.BeginLeftButtonDrag(new Point2D(100, 100), DrawingTool.Pen, _ => { });
        for (var i = 101; i <= 150; i++)
        {
            controller.UpdateDrag(new Point2D(i, Math.Cos(i) * 10), shift: false);
        }

        Assert.True(controller.CompleteDrag(session));
        Assert.Equal(2, session.Objects.Count);
        Assert.NotEqual(firstId, session.Objects[1].Id);
        Assert.False(controller.IsDrawing);
        Assert.Null(session.PreviewObject);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(100)]
    public void SequentialPenStrokesDoNotBleedLivePoints(int strokeCount)
    {
        using var session = new DrawingSession { ActiveTool = DrawingTool.Pen };
        var controller = new OverlayPointerInputController(session, MonitorA);
        for (var stroke = 0; stroke < strokeCount; stroke++)
        {
            controller.BeginLeftButtonDrag(new Point2D(stroke, stroke), DrawingTool.Pen, _ => { });
            controller.UpdateDrag(new Point2D(stroke + 20, stroke + 10), shift: false);
            Assert.True(controller.CompleteDrag(session));
            Assert.False(controller.IsDrawing);
            Assert.Equal(0, controller.LivePointCount);
            Assert.Null(session.PreviewObject);
        }

        Assert.Equal(strokeCount, session.Objects.Count);
    }
}

public class OverlayExtendedStyleVerifierTests
{
    [Fact]
    public void ClickThroughStyleIsDetected()
    {
        const long clickThrough = WindowStyles.WsExTransparent | WindowStyles.WsExTopmost;
        Assert.True(OverlayExtendedStyleVerifier.IsClickThroughEnabled(clickThrough));
    }

    [Fact]
    public void DrawingStyleFlagsExcludeTransparent()
    {
        const long drawingStyle = WindowStyles.WsExToolwindow | WindowStyles.WsExTopmost;
        Assert.False(OverlayExtendedStyleVerifier.IsClickThroughEnabled(drawingStyle));
    }
}

[Collection("WpfSta")]
public class OverlayInputCaptureIntegrationTests
{
    private static MonitorInfo CreateTestMonitor(double dpiX = 96, double dpiY = 96)
    {
        var monitors = MonitorEnumerator.EnumerateActiveMonitors();
        if (monitors.Count > 0)
        {
            var primary = monitors[0];
            return new MonitorInfo(
                @"\\.\DISPLAY_TEST",
                primary.Left + 40,
                primary.Top + 40,
                280,
                200,
                dpiX,
                dpiY);
        }

        return new MonitorInfo(@"\\.\DISPLAY_TEST", 180, 180, 280, 200, dpiX, dpiY);
    }

    [Fact]
    public void WindowFromPointReturnsOverlayHwnd()
    {
        RunSta(() =>
        {
            using var session = new DrawingSession();
            var monitor = CreateTestMonitor();
            var overlay = new OverlayWindow(monitor, session);
            Assert.True(overlay.InitializeInput(), overlay.InputDiagnosticState.ToPrivacySafeLine());

            var handle = new System.Windows.Interop.WindowInteropHelper(overlay).Handle;
            _ = OverlayExtendedStyleVerifier.ApplyDrawingInputStyles(handle);
            _ = NativeMethods.SetForegroundWindow(handle);
            Assert.True(NativeMethods.GetWindowRect(handle, out var rect));
            var centerX = rect.Left + 5;
            var centerY = rect.Top + 5;
            var pointHwnd = NativeMethods.WindowFromPoint(new NativeMethods.NativePoint { X = centerX, Y = centerY });
            Assert.True(pointHwnd == handle, $"Expected overlay hwnd 0x{handle:X}, WindowFromPoint returned 0x{pointHwnd:X}");

            var style = OverlayExtendedStyleVerifier.VerifyNoClickThrough(handle);
            Assert.True(style.Success);
            Assert.False(style.State.HasTransparent);

            overlay.ReleaseInputCapture();
            overlay.Close();
            WpfTestContext.Pump();
        });
    }

    [Fact]
    public void EmptyAreaVisualHitTestHitsInputSurface()
    {
        RunSta(() =>
        {
            using var session = new DrawingSession();
            var overlay = new OverlayWindow(CreateTestMonitor(), session);
            Assert.True(overlay.InitializeInput(), overlay.InputDiagnosticState.ToPrivacySafeLine());

            var point = new System.Windows.Point(overlay.InputSurface.ActualWidth / 2, overlay.InputSurface.ActualHeight / 2);
            Assert.True(OverlayVisualHitTesting.EmptyAreaHitsInputSurface(overlay.InputSurface, point));

            overlay.Close();
            WpfTestContext.Pump();
        });
    }

    [Fact]
    public void EscapeReleasesMouseCapture()
    {
        RunSta(() =>
        {
            using var session = new DrawingSession { ActiveTool = DrawingTool.Pen };
            var overlay = new OverlayWindow(CreateTestMonitor(), session);
            Assert.True(overlay.InitializeInput(), overlay.InputDiagnosticState.ToPrivacySafeLine());

            Mouse.Capture(overlay.InputSurface);
            Assert.Same(overlay.InputSurface, Mouse.Captured);

            overlay.RaiseEvent(new KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, System.Windows.Input.Keyboard.PrimaryDevice.ActiveSource, 0, Key.Escape)
            {
                RoutedEvent = UIElement.PreviewKeyDownEvent,
            });
            WpfTestContext.Pump();
            Assert.Null(Mouse.Captured);

            overlay.Close();
            WpfTestContext.Pump();
        });
    }

    [Fact]
    public void DeactivationDuringDragReleasesCapture()
    {
        RunSta(() =>
        {
            using var session = new DrawingSession();
            var overlay = new OverlayWindow(CreateTestMonitor(), session);
            Assert.True(overlay.InitializeInput(), overlay.InputDiagnosticState.ToPrivacySafeLine());

            Mouse.Capture(overlay.InputSurface);
            Assert.Same(overlay.InputSurface, Mouse.Captured);
            overlay.ReleaseInputCapture();
            Assert.Null(Mouse.Captured);

            overlay.Close();
            WpfTestContext.Pump();
        });
    }

    [Fact]
    public void MixedDpiOverlayInputSurfaceFillsClient()
    {
        RunSta(() =>
        {
            using var session = new DrawingSession();
            var overlay = new OverlayWindow(CreateTestMonitor(dpiX: 120, dpiY: 120), session);
            Assert.True(overlay.InitializeInput(), overlay.InputDiagnosticState.ToPrivacySafeLine());
            Assert.True(overlay.InputSurface.ActualWidth > 0);
            Assert.True(overlay.InputSurface.ActualHeight > 0);
            Assert.InRange(overlay.InputSurface.ActualWidth, overlay.Width - 2, overlay.Width + 2);
            Assert.InRange(overlay.InputSurface.ActualHeight, overlay.Height - 2, overlay.Height + 2);

            overlay.Close();
            WpfTestContext.Pump();
        });
    }

    [Fact]
    public void ClickThroughStyleCausesActivationFailure()
    {
        RunSta(() =>
        {
            using var session = new DrawingSession();
            var overlay = new OverlayWindow(CreateTestMonitor(), session);
            Assert.True(overlay.InitializeInput(), overlay.InputDiagnosticState.ToPrivacySafeLine());

            var handle = new System.Windows.Interop.WindowInteropHelper(overlay).Handle;
            var style = WindowStyles.GetWindowLongPtr(handle, WindowStyles.GwlExstyle);
            style = (nint)((long)style | WindowStyles.WsExTransparent);
            _ = WindowStyles.SetWindowLongPtr(handle, WindowStyles.GwlExstyle, style);

            var verify = OverlayExtendedStyleVerifier.VerifyNoClickThrough(handle);
            Assert.False(verify.Success);

            overlay.Close();
            WpfTestContext.Pump();
        });
    }

    [Fact]
    public void DisplayRebuildCreatesNewInputSurface()
    {
        RunSta(() =>
        {
            var coordinator = new OverlayCoordinator();
            using var session = new DrawingSession();
            var settings = AppSettings.CreateDefault();
            var temp = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            SettingsService.UseStoreForTesting(new JsonFileSettingsStore(temp));
            try
            {
                using var persistence = new SettingsPersistenceCoordinator(settings);
                Assert.True(coordinator.Activate(session, settings, persistence, () => { }));
                coordinator.RebuildOverlays(session, settings, () => { });
                Assert.Contains("CreateNewOverlays", coordinator.RebuildCallLog);
            }
            finally
            {
                coordinator.Deactivate();
                SettingsService.ResetStoreForTesting();
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

[Collection("WpfSta")]
public class DrawingSurfaceRenderTests
{
    [Fact]
    public void UnloadRemovesSessionChangedSubscription()
    {
        RunSta(() =>
        {
            using var session = new DrawingSession();
            var surface = new Faktum.ScreenMarker.App.Drawing.DrawingSurface(@"\\.\DISPLAY1", session);
            var host = new System.Windows.Controls.Grid { Width = 200, Height = 200 };
            host.Children.Add(surface);
            var window = new System.Windows.Window
            {
                Width = 220,
                Height = 220,
                Content = host,
                ShowInTaskbar = false,
            };
            window.Show();
            WpfTestContext.Pump();
            host.Children.Remove(surface);
            WpfTestContext.Pump();
            session.BeginPreview(@"\\.\DISPLAY1", new LineAnnotation(session.AllocateId(), @"\\.\DISPLAY1", StrokeStyle.DefaultPen, new Point2D(0, 0), new Point2D(10, 10)));
            window.Close();
            WpfTestContext.Pump();
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

public class WindowPositionHelperTests
{
    [Fact]
    public void EnsureTopmostBandReturnsFalseForZeroHandle()
    {
        Assert.False(WindowPositionHelper.EnsureTopmostBand(0));
    }
}

[Collection("WpfSta")]
public class ToolbarHitTestingIntegrationTests
{
    [Fact]
    public void ToolbarHitTargetsSurviveDrawingAndToolSwitch()
    {
        RunSta(() =>
        {
            var coordinator = new OverlayCoordinator();
            using var session = new DrawingSession();
            var settings = AppSettings.CreateDefault();
            var temp = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            SettingsService.UseStoreForTesting(new JsonFileSettingsStore(temp));
            try
            {
                using var persistence = new SettingsPersistenceCoordinator(settings);
                Assert.True(coordinator.Activate(session, settings, persistence, () => { }));

                var toolbar = coordinator.Toolbar!;
                var overlay = coordinator.Overlays.First(o => o.ToolbarHost.Toolbar is not null);

                Assert.True(coordinator.VerifyToolbarHitTesting());

                session.BeginPreview(overlay.Monitor.DeviceName, new FreehandStroke(
                    session.AllocateId(),
                    overlay.Monitor.DeviceName,
                    session.ActiveStyle,
                    [new Point2D(12, 12), new Point2D(40, 40)]));
                session.CommitPreview();

                Assert.True(coordinator.VerifyToolbarHitTesting());

                var lineButton = toolbar.FindControlByAutomationId(ToolbarControlIds.Tool.Line);
                Assert.NotNull(lineButton);
                ToolbarRoutedInput.InvokeAtCenter(lineButton, overlay.OverlayRoot);
                WpfTestContext.Pump();
                Assert.Equal(DrawingTool.Line, session.ActiveTool);
                Assert.True(toolbar.IsToolButtonSelected(DrawingTool.Line));
                Assert.True(OverlayVisualHitTesting.VerifyEmptyAreaHitsInputSurface(overlay, out _));
            }
            finally
            {
                coordinator.Deactivate();
                SettingsService.ResetStoreForTesting();
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

[Collection("WpfSta")]
public class ToolbarToolSelectionIntegrationTests
{
    [Fact]
    public void SequentialToolSelectionInOneSessionPreservesDrawings()
    {
        RunSta(() =>
        {
            var coordinator = new OverlayCoordinator();
            using var session = new DrawingSession();
            var settings = AppSettings.CreateDefault();
            var temp = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            SettingsService.UseStoreForTesting(new JsonFileSettingsStore(temp));
            try
            {
                using var persistence = new SettingsPersistenceCoordinator(settings);
                Assert.True(coordinator.Activate(session, settings, persistence, () => { }));
                var toolbar = coordinator.Toolbar!;
                var pointerOverlay = coordinator.Overlays.First(o => o.ToolbarHost.Toolbar is not null);
                var monitor = coordinator.Overlays[0].Monitor.DeviceName;

                var sequence = new[]
                {
                    DrawingTool.Pen,
                    DrawingTool.Line,
                    DrawingTool.Arrow,
                    DrawingTool.Rectangle,
                    DrawingTool.Ellipse,
                    DrawingTool.Eraser,
                    DrawingTool.Pen,
                    DrawingTool.Rectangle,
                };

                foreach (var tool in sequence)
                {
                    var automationId = ToolAutomationIdFor(tool);
                    var button = toolbar.FindControlByAutomationId(automationId);
                    Assert.NotNull(button);
                    ToolbarRoutedInput.InvokeAtCenter(button, pointerOverlay.OverlayRoot);
                    WpfTestContext.Pump();
                    Assert.Equal(tool, session.ActiveTool);
                    Assert.Equal(1, toolbar.SelectedToolButtonCount);
                }

                session.BeginPreview(monitor, new FreehandStroke(session.AllocateId(), monitor, session.ActiveStyle, [new Point2D(1, 1), new Point2D(5, 5)]));
                session.CommitPreview();
                session.BeginPreview(monitor, new LineAnnotation(session.AllocateId(), monitor, session.ActiveStyle, new Point2D(10, 10), new Point2D(20, 20)));
                session.CommitPreview();
                Assert.Equal(2, session.Objects.Count);
            }
            finally
            {
                coordinator.Deactivate();
                SettingsService.ResetStoreForTesting();
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
        });
    }

    private static string ToolAutomationIdFor(DrawingTool tool) =>
        tool switch
        {
            DrawingTool.Pen => ToolbarControlIds.Tool.Pen,
            DrawingTool.Line => ToolbarControlIds.Tool.Line,
            DrawingTool.Arrow => ToolbarControlIds.Tool.Arrow,
            DrawingTool.Rectangle => ToolbarControlIds.Tool.Rectangle,
            DrawingTool.Ellipse => ToolbarControlIds.Tool.Ellipse,
            DrawingTool.Text => ToolbarControlIds.Tool.Text,
            DrawingTool.Eraser => ToolbarControlIds.Tool.Eraser,
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null),
        };

    [Fact]
    public void SequentialDrawingTypesMatchActiveToolInOneSession()
    {
        RunSta(() =>
        {
            using var session = new DrawingSession();
            var monitor = @"\\.\DISPLAY1";
            var toolsAndTypes = new (DrawingTool Tool, Func<DrawingObject> Create, Type ExpectedType)[]
            {
                (DrawingTool.Pen, () => new FreehandStroke(session.AllocateId(), monitor, session.ActiveStyle, [new Point2D(0, 0), new Point2D(1, 1)]), typeof(FreehandStroke)),
                (DrawingTool.Line, () => new LineAnnotation(session.AllocateId(), monitor, session.ActiveStyle, new Point2D(0, 0), new Point2D(2, 2)), typeof(LineAnnotation)),
                (DrawingTool.Arrow, () => new ArrowAnnotation(session.AllocateId(), monitor, session.ActiveStyle, new Point2D(0, 0), new Point2D(2, 2)), typeof(ArrowAnnotation)),
                (DrawingTool.Rectangle, () => new RectangleAnnotation(session.AllocateId(), monitor, session.ActiveStyle, new Rect2D(0, 0, 4, 4)), typeof(RectangleAnnotation)),
                (DrawingTool.Ellipse, () => new EllipseAnnotation(session.AllocateId(), monitor, session.ActiveStyle, new Rect2D(0, 0, 4, 4)), typeof(EllipseAnnotation)),
            };

            foreach (var entry in toolsAndTypes)
            {
                session.ActiveTool = entry.Tool;
                session.BeginPreview(monitor, entry.Create());
                session.CommitPreview();
                Assert.IsType(entry.ExpectedType, session.Objects[^1]);
            }

            Assert.Equal(toolsAndTypes.Length, session.Objects.Count);
        });
    }

    [Fact]
    public void RapidPenLineSwitchMaintainsSingleSelection()
    {
        RunSta(() =>
        {
            var coordinator = new OverlayCoordinator();
            using var session = new DrawingSession();
            var settings = AppSettings.CreateDefault();
            var temp = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            SettingsService.UseStoreForTesting(new JsonFileSettingsStore(temp));
            try
            {
                using var persistence = new SettingsPersistenceCoordinator(settings);
                Assert.True(coordinator.Activate(session, settings, persistence, () => { }));
                var toolbar = coordinator.Toolbar!;
                var pointerOverlay = coordinator.Overlays.First(o => o.ToolbarHost.Toolbar is not null);
                Assert.Equal(DrawingTool.Pen, session.ActiveTool);
                Assert.True(toolbar.IsToolButtonSelected(DrawingTool.Pen));
                Assert.Equal(7, toolbar.RegisteredToolButtonCount);

                session.BeginPreview(coordinator.Overlays[0].Monitor.DeviceName, new FreehandStroke(
                    session.AllocateId(),
                    coordinator.Overlays[0].Monitor.DeviceName,
                    session.ActiveStyle,
                    [new Point2D(2, 2), new Point2D(8, 8)]));
                session.CommitPreview();

                for (var i = 0; i < 100; i++)
                {
                    var tool = i % 2 == 0 ? DrawingTool.Line : DrawingTool.Pen;
                    var automationId = tool == DrawingTool.Line ? ToolbarControlIds.Tool.Line : ToolbarControlIds.Tool.Pen;
                    var button = toolbar.FindControlByAutomationId(automationId);
                    Assert.NotNull(button);
                    ToolbarRoutedInput.InvokeAtCenter(button, pointerOverlay.OverlayRoot);
                    WpfTestContext.Pump();
                    Assert.Equal(tool, session.ActiveTool);
                    Assert.Equal(1, toolbar.SelectedToolButtonCount);
                }

                Assert.Single(session.Objects);
            }
            finally
            {
                coordinator.Deactivate();
                SettingsService.ResetStoreForTesting();
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
        });
    }

    [Fact]
    public void PenIsDefaultSelectedToolOnActivation()
    {
        RunSta(() =>
        {
            var coordinator = new OverlayCoordinator();
            using var session = new DrawingSession();
            var settings = AppSettings.CreateDefault();
            var temp = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            SettingsService.UseStoreForTesting(new JsonFileSettingsStore(temp));
            try
            {
                using var persistence = new SettingsPersistenceCoordinator(settings);
                Assert.True(coordinator.Activate(session, settings, persistence, () => { }));
                var toolbar = coordinator.Toolbar!;
                Assert.Equal(DrawingTool.Pen, session.ActiveTool);
                Assert.True(toolbar.IsToolButtonSelected(DrawingTool.Pen));
                Assert.Equal(1, toolbar.SelectedToolButtonCount);
            }
            finally
            {
                coordinator.Deactivate();
                SettingsService.ResetStoreForTesting();
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}


[Collection("WpfSta")]
public class DualMonitorToolbarIntegrationTests
{
    [Fact]
    public void DualMonitorActivationSharesToolSelectionWhenTwoMonitorsPresent()
    {
        var monitors = MonitorEnumerator.EnumerateActiveMonitors();
        if (monitors.Count < 2)
        {
            return;
        }

        RunSta(() =>
        {
            var coordinator = new OverlayCoordinator();
            using var session = new DrawingSession();
            var settings = AppSettings.CreateDefault();
            var temp = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            SettingsService.UseStoreForTesting(new JsonFileSettingsStore(temp));
            try
            {
                using var persistence = new SettingsPersistenceCoordinator(settings);
                Assert.True(coordinator.Activate(session, settings, persistence, () => { }));
                Assert.Equal(2, coordinator.Overlays.Count);

                var arrowButton = coordinator.Toolbar!.FindControlByAutomationId(ToolbarControlIds.Tool.Arrow);
                Assert.NotNull(arrowButton);
                ToolbarRoutedInput.InvokeAtCenter(arrowButton, coordinator.Overlays.First(o => o.ToolbarHost.Toolbar is not null).OverlayRoot);
                WpfTestContext.Pump();
                Assert.Equal(DrawingTool.Arrow, session.ActiveTool);
                Assert.True(coordinator.VerifyToolbarHitTesting());

                var firstMonitor = coordinator.Overlays[0].Monitor.DeviceName;
                var secondMonitor = coordinator.Overlays[1].Monitor.DeviceName;
                session.BeginPreview(firstMonitor, new LineAnnotation(session.AllocateId(), firstMonitor, session.ActiveStyle, new Point2D(1, 1), new Point2D(5, 5)));
                session.CommitPreview();
                session.BeginPreview(secondMonitor, new LineAnnotation(session.AllocateId(), secondMonitor, session.ActiveStyle, new Point2D(2, 2), new Point2D(6, 6)));
                session.CommitPreview();
                Assert.Equal(2, session.Objects.Count);

                coordinator.RebuildOverlays(session, settings, () => { });
                Assert.Equal(DrawingTool.Arrow, session.ActiveTool);
                Assert.True(coordinator.VerifyToolbarHitTesting());
            }
            finally
            {
                coordinator.Deactivate();
                SettingsService.ResetStoreForTesting();
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}
