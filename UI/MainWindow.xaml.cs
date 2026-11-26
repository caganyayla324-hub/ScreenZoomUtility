using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Engines.Keyboard;
using ScreenZoomUtility.Engines.Zoom;
using ScreenZoomUtility.Models;
using ScreenZoomUtility.UI.Views;

namespace ScreenZoomUtility.UI;

public partial class MainWindow : Window
{
    private readonly ConfigurationManager _config;
    private readonly SafetyManager _safety;
    private readonly ZoomEngine _zoom;
    private readonly HotkeyManager _hotkeys;
    private readonly SequenceExecutor _executor;
    private readonly PerformanceMonitor _perf;

    private readonly DashboardView _dashboardView;
    private readonly ScreenZoomView _zoomView;
    private readonly KeyboardUtilityView _keyboardView;
    private readonly HotkeyManagerView _hotkeyView;
    private readonly ProfilesView _profilesView;
    private readonly SettingsView _settingsView;
    private readonly AboutView _aboutView;

    public MainWindow(
        ConfigurationManager config,
        SafetyManager safety,
        ZoomEngine zoom,
        HotkeyManager hotkeys,
        SequenceExecutor executor,
        PerformanceMonitor perf)
    {
        InitializeComponent();

        _config = config;
        _safety = safety;
        _zoom = zoom;
        _hotkeys = hotkeys;
        _executor = executor;
        _perf = perf;

        _dashboardView = new DashboardView(_config, _safety, _zoom, _executor, _perf);
        _zoomView = new ScreenZoomView(_config, _zoom);
        _keyboardView = new KeyboardUtilityView(_config, _safety, _executor, RefreshAllBindings);
        _hotkeyView = new HotkeyManagerView(_config, _hotkeys, RefreshAllBindings);
        _profilesView = new ProfilesView(_config, _zoom, OnProfileChanged);
        _settingsView = new SettingsView(_config, _zoom, RefreshAllBindings);
        _aboutView = new AboutView();

        _zoom.StateChanged += () => Dispatcher.Invoke(UpdateTopBadges);
        _safety.SequenceActivityChanged += (running, name) => Dispatcher.Invoke(() =>
        {
            ActiveSequenceBadge.Visibility = running && _config.Settings.Safety.ShowActiveIndicator
                ? Visibility.Visible
                : Visibility.Collapsed;
            ActiveSequenceText.Text = running ? $"ACTIVE SEQUENCE: {name}" : string.Empty;
        });

        _hotkeys.HotkeyPressed += OnGlobalHotkeyPressed;

        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;

        MainContentHost.Content = _dashboardView;
        UpdateTopBadges();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hotkeys.AttachToWindow(handle);
        _zoom.Initialize();
        RefreshAllBindings();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_config.Settings.ConfirmOnExit)
        {
            var res = MessageBox.Show("Exit Screen Zoom & Keyboard Utility?", "Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        _safety.TriggerEmergencyStop("Application closing");
        _zoom.EmergencyReset();
        _hotkeys.UnregisterAll();
        _config.SaveAll();
    }

    private void RefreshAllBindings()
    {
        var allBindings = GetEffectiveHotkeys();
        _hotkeys.ApplyBindings(allBindings);

        var emergency = allBindings.FirstOrDefault(h => h.Id == "safety.emergency_stop");
        EmergencyShortcutText.Text = $"Shortcut: {emergency?.FormattedShortcut ?? "Ctrl + Alt + Esc"}";

        UpdateTopBadges();
        _dashboardView.RefreshView();
        _hotkeyView.RefreshView();
    }

    private void OnProfileChanged()
    {
        _zoom.ApplyProfileSettings(_config.Settings.Zoom);
        RefreshAllBindings();
        _zoomView.RefreshView();
        _keyboardView.RefreshView();
        _profilesView.RefreshView();
    }

    private List<HotkeyBinding> GetEffectiveHotkeys()
    {
        return _config.ActiveProfile.Hotkeys;
    }

    private async void OnGlobalHotkeyPressed(HotkeyBinding binding)
    {
        switch (binding.Id)
        {
            case "safety.emergency_stop":
                _safety.TriggerEmergencyStop("Emergency Stop Hotkey");
                break;
            case "zoom.toggle":
                _zoom.Toggle();
                break;
            case "zoom.activate":
                _zoom.Activate();
                break;
            case "zoom.deactivate":
                _zoom.Deactivate();
                break;
            case "zoom.increase":
                _zoom.IncreaseZoom();
                break;
            case "zoom.decrease":
                _zoom.DecreaseZoom();
                break;
            case "zoom.reset":
                _zoom.ResetZoom();
                break;
            case "zoom.next_profile":
                _config.SwitchToNextProfile();
                OnProfileChanged();
                break;
            default:
                if (!string.IsNullOrWhiteSpace(binding.TargetSequenceId))
                {
                    var seq = _config.ActiveProfile.Sequences.FirstOrDefault(s => s.Id == binding.TargetSequenceId);
                    if (seq != null && seq.Enabled)
                    {
                        await _executor.ExecuteAsync(seq);
                    }
                }
                break;
        }
    }

    private void UpdateTopBadges()
    {
        TopProfileBadge.Text = $"Profile: {_config.ActiveProfile.Name}";
        TopZoomBadge.Text = _zoom.IsZoomActive
            ? $"Zoom: {_zoom.TargetMultiplier:0.0}x (ACTIVE)"
            : "Zoom: 1.0x (Idle)";
    }

    private void OnEmergencyStopClick(object sender, RoutedEventArgs e)
    {
        _safety.TriggerEmergencyStop("Emergency Stop Button");
    }

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (MainContentHost == null || sender is not RadioButton rb) return;

        switch (rb.Tag?.ToString())
        {
            case "Dashboard":
                HeaderTitle.Text = "Dashboard";
                _dashboardView.RefreshView();
                MainContentHost.Content = _dashboardView;
                break;
            case "Zoom":
                HeaderTitle.Text = "Screen Zoom Engine";
                _zoomView.RefreshView();
                MainContentHost.Content = _zoomView;
                break;
            case "Keyboard":
                HeaderTitle.Text = "Keyboard Utility & Sequence Editor";
                _keyboardView.RefreshView();
                MainContentHost.Content = _keyboardView;
                break;
            case "Hotkeys":
                HeaderTitle.Text = "Hotkey Manager";
                _hotkeyView.RefreshView();
                MainContentHost.Content = _hotkeyView;
                break;
            case "Profiles":
                HeaderTitle.Text = "Profiles";
                _profilesView.RefreshView();
                MainContentHost.Content = _profilesView;
                break;
            case "Settings":
                HeaderTitle.Text = "Settings & Safety Limits";
                _settingsView.RefreshView();
                MainContentHost.Content = _settingsView;
                break;
            case "About":
                HeaderTitle.Text = "About & Compliance";
                MainContentHost.Content = _aboutView;
                break;
        }
    }
}
