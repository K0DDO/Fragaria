using Fragaria.Models;
using Fragaria.Services;
using Fragaria.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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

    public MainWindow()
    {
        InitializeComponent();
        Title = "Fragaria";

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

        WindowPicker.WindowSelected += hwnd => _vm.AddWindow(hwnd);

        NoiseGateToggle.Toggled += (s, _) => _vm.NoiseGateEnabled = ((ToggleSwitch)s!).IsOn;
        DuckingToggle.Toggled += (s, _) => _vm.DuckingEnabled = ((ToggleSwitch)s!).IsOn;
        ObsToggle.Toggled += (s, _) =>
        {
            var on = ((ToggleSwitch)s!).IsOn;
            _vm.ObsEnabled = on;
            if (_obs != null)
            {
                if (on) _ = _obs.ConnectAsync();
                else _ = _obs.DisconnectAsync();
            }
        };

        _vm.StartEngine();
        _vm.RefreshStrips();
        RefreshWindowPicker();

        SetupTray();
        SetupHotkeys();
        SetupObs();

        Closed += (_, _) => Cleanup();
    }

    private void SetupTray()
    {
        _tray = new TrayService();
        _tray.OpenRequested += () => DispatcherQueue.TryEnqueue(() => { AppWindow.Show(); Activate(); });
        _tray.ExitRequested += () => DispatcherQueue.TryEnqueue(Close);
        _tray.ShowBalloon("Fragaria", "Все 10 функций активны!");
    }

    private void SetupHotkeys()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        _hotkeys = new GlobalHotkeyService(hwnd);
        _hotkeys.RegisterScenes(_vm.Scenes, scene => DispatcherQueue.TryEnqueue(() =>
        {
            _vm.ApplySceneCommand.Execute(scene);
            StatusText.Text = $"Сцена: {scene.Name}";
        }));
        _hook = new HwndMessageHook(hwnd, _hotkeys);
    }

    private void SetupObs()
    {
        _obs = new ObsWebSocketService(_vm.Settings.Obs);
        _obs.ConnectionChanged += c => DispatcherQueue.TryEnqueue(() =>
            ObsStatus.Text = c ? "OBS: подключён" : "OBS: отключён");
        _obs.SceneChanged += scene => DispatcherQueue.TryEnqueue(() =>
        {
            _engine.OnObsScene(scene, _vm.Settings.Obs);
            StatusText.Text = $"OBS сцена: {scene}";
        });
        if (_vm.ObsEnabled) _ = _obs.ConnectAsync();
    }

    private void RefreshWindowPicker() =>
        WindowPicker.SetWindows(_vm.GetWindows());

    private void UpdateMeters()
    {
        MasterHpSpectrum.SetBands(_vm.HpSpectrum);
        MasterStreamSpectrum.SetBands(_vm.StreamSpectrum);
        DuckingLabel.Text = $"Duck: {_vm.DuckingGain:F0}%";
        if (!string.IsNullOrEmpty(_vm.StatusMessage))
            StatusText.Text = _vm.StatusMessage;
        RecordBtn.Content = _vm.IsRecording ? "⏹ Стоп" : "⏺ Запись";
    }

    private void Cleanup()
    {
        _meterTimer.Stop();
        _windowTimer.Stop();
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

    private void SceneGame_Click(object sender, RoutedEventArgs e) =>
        ApplySceneByIndex(0);

    private void SceneTalk_Click(object sender, RoutedEventArgs e) =>
        ApplySceneByIndex(1);

    private void SceneStream_Click(object sender, RoutedEventArgs e) =>
        ApplySceneByIndex(2);

    private void ApplySceneByIndex(int i)
    {
        if (i < _vm.Scenes.Count)
            _vm.ApplySceneCommand.Execute(_vm.Scenes[i]);
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            Title = "Настройки Fragaria",
            PrimaryButtonText = "OK",
            XamlRoot = Content.XamlRoot,
            Content = BuildSettingsPanel()
        };
        await dlg.ShowAsync();
    }

    private StackPanel BuildSettingsPanel()
    {
        var vdm = new VirtualDriverManager(_vm.Settings.VirtualDriver);
        var status = vdm.CheckStatus();
        return new StackPanel
        {
            Spacing = 12,
            Width = 400,
            Children =
            {
                new TextBlock { Text = "Noise Gate", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                new Slider { Header = "Порог %", Minimum = 0, Maximum = 10, Value = _vm.NoiseGateThreshold },
                new TextBlock { Text = "Ducking", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                new Slider { Header = "Сила ducking %", Minimum = 0, Maximum = 80, Value = _vm.DuckingAmount },
                new TextBlock { Text = "Запись", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                new CheckBox { Content = "Записывать шину A", IsChecked = _vm.RecordHeadphones },
                new CheckBox { Content = "Записывать шину B", IsChecked = _vm.RecordStream },
                new TextBlock { Text = "OBS WebSocket", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                new TextBox { PlaceholderText = "ws://127.0.0.1:4455", Text = _vm.ObsHost },
                new TextBlock { Text = "Виртуальный драйвер", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                new TextBlock
                {
                    Text = status.HasOutputA
                        ? "✓ Виртуальные устройства обнаружены"
                        : "✗ Установите VB-Cable (см. driver/install-fragaria-driver.ps1)",
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
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
