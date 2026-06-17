using Fragaria.Models;
using Fragaria.Services;
using Fragaria.ViewModels;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Linq;
using WinRT.Interop;

namespace Fragaria;

public sealed partial class MainWindow : Window
{
    private readonly MixerEngine _engine = new();
    private readonly MainViewModel _vm;
    private readonly SettingsService _settings = new();
    private TrayService? _tray;
    private GlobalHotkeyService? _hotkeys;
    private HwndMessageHook? _hook;
    private ObsWebSocketService? _obs;
    private readonly DispatcherTimer _meterTimer;
    private readonly DispatcherTimer _windowTimer;
    private readonly DispatcherTimer _clockTimer;
    private DateTime _lastCpuCheck = DateTime.UtcNow;
    private TimeSpan _lastCpuTime;
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Fragaria";
        ConfigureWindow();

        var dq = DispatcherQueue.GetForCurrentThread();
        _vm = new MainViewModel(_engine, dq);
        StripsList.ItemsSource = _vm.Strips;

        foreach (var p in _vm.Presets) PresetCombo.Items.Add(p);
        if (PresetCombo.Items.Count > 0) PresetCombo.SelectedIndex = 0;

        MasterHpSlider.Value = 100;
        MasterStreamSlider.Value = 100;
        MasterHpLimitSlider.Value = 100;
        MasterStreamLimitSlider.Value = 100;

        AutoStartToggle.IsOn = AutoStartService.IsEnabled();
        TrayToggle.IsOn = _settings.Load().MinimizeToTray;
        NoiseGateToggle.IsOn = _vm.NoiseGateEnabled;
        DuckingToggle.IsOn = _vm.DuckingEnabled;
        ObsToggle.IsOn = _vm.ObsEnabled;

        _meterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _meterTimer.Tick += (_, _) => UpdateMeters();
        _meterTimer.Start();

        _windowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _windowTimer.Tick += (_, _) => RefreshWindowPicker();
        _windowTimer.Start();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => ClockLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();
        ClockLabel.Text = DateTime.Now.ToString("HH:mm:ss");

        WindowPicker.WindowSelected += hwnd => _vm.AddWindow(hwnd);

        NoiseGateToggle.Toggled += (s, _) => _vm.NoiseGateEnabled = ((ToggleSwitch)s!).IsOn;
        DuckingToggle.Toggled += (s, _) => _vm.DuckingEnabled = ((ToggleSwitch)s!).IsOn;
        ObsToggle.Toggled += async (s, _) =>
        {
            var on = ((ToggleSwitch)s!).IsOn;
            _vm.ObsEnabled = on;
            if (_obs == null) return;
            if (on) await _obs.ConnectAsync();
            else await _obs.DisconnectAsync();
        };

        WireEffectsSliders();
        PopulateDeviceCombos();

        Activated += async (_, _) =>
        {
            if (_initialized) return;
            _initialized = true;
            await InitializeAppAsync();
        };

        Closed += (_, _) => Cleanup();
    }

    private async Task InitializeAppAsync()
    {
        try
        {
            var installDir = Path.GetDirectoryName(Environment.ProcessPath);
            _vm.SeedFromInstallDir(installDir);
            foreach (var p in _vm.Presets)
                if (!PresetCombo.Items.Contains(p)) PresetCombo.Items.Add(p);

            if (!_vm.Settings.SetupCompleted)
                await ShowSetupWizardAsync();

            _vm.StartEngine();
            _vm.RefreshStrips();
            RefreshWindowPicker();

            SetupTray();
            try { SetupHotkeys(); } catch (Exception ex) { AppLogger.Error("Hotkeys failed", ex); }
            try { SetupObs(); } catch (Exception ex) { AppLogger.Error("OBS setup failed", ex); }

            if (!_vm.AudioReady)
                StatusText.Text = "Аудио ограничено — откройте Settings";
        }
        catch (Exception ex)
        {
            AppLogger.Error("InitializeAppAsync failed", ex);
            StatusText.Text = "Ошибка запуска — см. fragaria.log";
            await ShowErrorDialogAsync(ex);
        }
    }

    private async Task ShowSetupWizardAsync()
    {
        var hp = new ComboBox { Header = "A2 Monitor (наушники)", Width = 420 };
        var st = new ComboBox { Header = "A1 Stream (виртуальный выход)", Width = 420 };
        var mic = new ComboBox { Header = "Микрофон", Width = 420 };
        foreach (var d in AudioDeviceService.ListDevices(NAudio.CoreAudioApi.DataFlow.Render))
            hp.Items.Add(d);
        foreach (var d in AudioDeviceService.ListDevices(NAudio.CoreAudioApi.DataFlow.Render))
            st.Items.Add(d);
        foreach (var d in AudioDeviceService.ListDevices(NAudio.CoreAudioApi.DataFlow.Capture))
            mic.Items.Add(d);
        if (hp.Items.Count > 0) hp.SelectedIndex = 0;
        if (st.Items.Count > 0) st.SelectedIndex = Math.Min(1, st.Items.Count - 1);
        if (mic.Items.Count > 0) mic.SelectedIndex = 0;

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "Добро пожаловать в Fragaria! Выберите аудиоустройства.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(hp);
        panel.Children.Add(st);
        panel.Children.Add(mic);
        panel.Children.Add(new TextBlock
        {
            Text = "Совет: для OBS установите VB-Cable и выберите CABLE Input как Stream.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7
        });

        var dlg = new ContentDialog
        {
            Title = "Первый запуск",
            Content = panel,
            PrimaryButtonText = "Продолжить",
            XamlRoot = Content.XamlRoot
        };
        await dlg.ShowAsync();
        _vm.CompleteSetup(
            (hp.SelectedItem as AudioDeviceInfo)?.Id,
            (st.SelectedItem as AudioDeviceInfo)?.Id,
            (mic.SelectedItem as AudioDeviceInfo)?.Id);
    }

    private async Task ShowErrorDialogAsync(Exception ex)
    {
        var dlg = new ContentDialog
        {
            Title = "Fragaria — ошибка",
            Content = $"{ex.Message}\n\nЛог: {AppLogger.LogFilePath}",
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dlg.ShowAsync();
    }

    private void PopulateDeviceCombos()
    {
        SettingsHpCombo.Items.Clear();
        SettingsStreamCombo.Items.Clear();
        SettingsMicCombo.Items.Clear();
        foreach (var d in AudioDeviceService.ListDevices(NAudio.CoreAudioApi.DataFlow.Render))
        {
            SettingsHpCombo.Items.Add(d);
            SettingsStreamCombo.Items.Add(d);
        }
        foreach (var d in AudioDeviceService.ListDevices(NAudio.CoreAudioApi.DataFlow.Capture))
            SettingsMicCombo.Items.Add(d);
    }

    private void ConfigureWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(id);
        appWindow.Resize(new Windows.Graphics.SizeInt32(1320, 780));
        if (appWindow.TitleBar != null)
            appWindow.TitleBar.ExtendsContentIntoTitleBar = false;

        if (MicaController.IsSupported())
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
    }

    private void WireEffectsSliders()
    {
        FxEqLow.ValueChanged += (_, _) => ApplyFxToMic();
        FxEqMid.ValueChanged += (_, _) => ApplyFxToMic();
        FxEqHigh.ValueChanged += (_, _) => ApplyFxToMic();
        FxCompThreshold.ValueChanged += (_, _) => ApplyFxToMic();
        FxCompRatio.ValueChanged += (_, _) => ApplyFxToMic();
    }

    private void ApplyFxToMic()
    {
        var mic = _vm.Strips.FirstOrDefault(s => s.IsMicrophone);
        if (mic == null) return;
        mic.EqLow = FxEqLow.Value;
        mic.EqMid = FxEqMid.Value;
        mic.EqHigh = FxEqHigh.Value;
        mic.CompThreshold = FxCompThreshold.Value;
        mic.CompRatio = FxCompRatio.Value;
    }

    private void SyncEffectsFromMic()
    {
        var mic = _vm.Strips.FirstOrDefault(s => s.IsMicrophone);
        if (mic == null) return;
        FxEqLow.Value = mic.EqLow;
        FxEqMid.Value = mic.EqMid;
        FxEqHigh.Value = mic.EqHigh;
        FxCompThreshold.Value = mic.CompThreshold;
        FxCompRatio.Value = mic.CompRatio;
    }

    private void SetupTray()
    {
        try
        {
            _tray = new TrayService();
            _tray.OpenRequested += () => DispatcherQueue.TryEnqueue(() => { AppWindow.Show(); Activate(); });
            _tray.ExitRequested += () => DispatcherQueue.TryEnqueue(Close);
            _tray.ShowBalloon("Fragaria", "Pro Audio for Streamers");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Tray setup failed", ex);
        }
    }

    private void SetupHotkeys()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        _hotkeys = new GlobalHotkeyService(hwnd);
        _hotkeys.RegisterScenes(_vm.Scenes, scene => DispatcherQueue.TryEnqueue(() =>
        {
            _vm.ApplySceneCommand.Execute(scene);
            StatusText.Text = $"Scene: {scene.Name}";
        }));
        _hook = new HwndMessageHook(hwnd, _hotkeys);
    }

    private void SetupObs()
    {
        _obs = new ObsWebSocketService(_vm.Settings.Obs);
        _obs.ConnectionChanged += c => DispatcherQueue.TryEnqueue(() =>
            ObsStatus.Text = c ? "OBS ●" : "OBS ○");
        _obs.SceneChanged += scene => DispatcherQueue.TryEnqueue(() =>
        {
            _engine.OnObsScene(scene, _vm.Settings.Obs);
            StatusText.Text = $"OBS: {scene}";
        });
        if (_vm.ObsEnabled || _vm.Settings.Obs.AutoConnect) _ = _obs.ConnectAsync();
    }

    private void RefreshWindowPicker() =>
        WindowPicker.SetWindows(_vm.GetWindows());

    private void UpdateMeters()
    {
        MasterHpSpectrum.SetBands(_vm.HpSpectrum);
        MasterStreamSpectrum.SetBands(_vm.StreamSpectrum);
        EffectsSpectrum.SetBands(_vm.StreamSpectrum);
        MasterHpMeter.Level = _vm.MasterHpPeak;
        MasterStreamMeter.Level = _vm.MasterStreamPeak;
        DuckingLabel.Text = $"DUCK {_vm.DuckingGain:F0}%";
        if (!string.IsNullOrEmpty(_vm.StatusMessage))
            StatusText.Text = _vm.StatusMessage;
        RecordBtn.Content = _vm.IsRecording ? "STOP" : "REC";
        RecordIndicator.Text = _vm.IsRecording ? "ON" : "OFF";
        RecordIndicator.Foreground = _vm.IsRecording
            ? (Brush)Application.Current.Resources["FragariaPrimaryBrush"]
            : (Brush)Application.Current.Resources["FragariaMutedBrush"];
        UpdateCpuLabel();
    }

    private void UpdateCpuLabel()
    {
        var proc = Process.GetCurrentProcess();
        var now = DateTime.UtcNow;
        var cpu = proc.TotalProcessorTime;
        var elapsed = (now - _lastCpuCheck).TotalMilliseconds;
        if (elapsed > 400)
        {
            var usage = (cpu - _lastCpuTime).TotalMilliseconds / elapsed / Environment.ProcessorCount * 100;
            CpuLabel.Text = $"CPU {usage:F1}%";
            _lastCpuCheck = now;
            _lastCpuTime = cpu;
        }
    }

    private void Cleanup()
    {
        _meterTimer.Stop();
        _windowTimer.Stop();
        _clockTimer.Stop();
        _hotkeys?.Dispose();
        _hook?.Dispose();
        _obs?.Dispose();
        _tray?.Dispose();
        _engine.Dispose();
    }

    private void Master_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        _vm.MasterHp = MasterHpSlider.Value;
        _vm.MasterStream = MasterStreamSlider.Value;
        _vm.MasterHpLimit = MasterHpLimitSlider.Value;
        _vm.MasterStreamLimit = MasterStreamLimitSlider.Value;
        if (MasterMusicSlider != null)
            _vm.MasterMusic = MasterMusicSlider.Value;
    }

    private void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetCombo.SelectedItem is string name)
        { _vm.ActivePreset = name; _vm.LoadPresetCommand.Execute(null); }
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetCombo.SelectedItem is string name)
        { _vm.ActivePreset = name; _vm.SavePresetCommand.Execute(null); }
    }

    private void Rescan_Click(object sender, RoutedEventArgs e)
    {
        _vm.RescanWindowsCommand.Execute(null);
        RefreshWindowPicker();
    }

    private void Record_Click(object sender, RoutedEventArgs e) =>
        _vm.ToggleRecordingCommand.Execute(null);

    private void SceneGame_Click(object sender, RoutedEventArgs e) => ApplySceneByIndex(0);
    private void SceneTalk_Click(object sender, RoutedEventArgs e) => ApplySceneByIndex(1);
    private void SceneStream_Click(object sender, RoutedEventArgs e) => ApplySceneByIndex(2);

    private void ApplySceneByIndex(int i)
    {
        if (i < _vm.Scenes.Count)
            _vm.ApplySceneCommand.Execute(_vm.Scenes[i]);
    }

    private void ApplyDevices_Click(object sender, RoutedEventArgs e)
    {
        _vm.CompleteSetup(
            (SettingsHpCombo.SelectedItem as AudioDeviceInfo)?.Id,
            (SettingsStreamCombo.SelectedItem as AudioDeviceInfo)?.Id,
            (SettingsMicCombo.SelectedItem as AudioDeviceInfo)?.Id);
        _vm.RestartEngine();
        StatusText.Text = "Устройства применены";
    }

    private void NavMixer_Click(object sender, RoutedEventArgs e) => ShowPage(MixerPage, NavMixer);
    private void NavRouting_Click(object sender, RoutedEventArgs e) => ShowPage(RoutingPage, NavRouting);
    private void NavEffects_Click(object sender, RoutedEventArgs e)
    {
        ShowPage(EffectsPage, NavEffects);
        SyncEffectsFromMic();
    }
    private void NavSettings_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage, NavSettings);

    private void ShowPage(FrameworkElement page, Button activeBtn)
    {
        MixerPage.Visibility = Visibility.Collapsed;
        RoutingPage.Visibility = Visibility.Collapsed;
        EffectsPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;

        NavMixer.Style = (Style)Application.Current.Resources["FragariaNavBtn"];
        NavRouting.Style = (Style)Application.Current.Resources["FragariaNavBtn"];
        NavEffects.Style = (Style)Application.Current.Resources["FragariaNavBtn"];
        NavSettings.Style = (Style)Application.Current.Resources["FragariaNavBtn"];
        activeBtn.Style = (Style)Application.Current.Resources["FragariaNavBtnActive"];
    }

    private void Strip_DragOver(object sender, DragEventArgs e) =>
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;

    private async void Strip_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            var text = await e.DataView.GetTextAsync();
            if (long.TryParse(text, out var h))
                _vm.AddWindow((nint)h);
        }
    }
}
