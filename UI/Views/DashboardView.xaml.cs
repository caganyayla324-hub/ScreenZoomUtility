using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Engines.Keyboard;
using ScreenZoomUtility.Engines.Zoom;

namespace ScreenZoomUtility.UI.Views;

public partial class DashboardView : UserControl
{
    private readonly ConfigurationManager _config;
    private readonly SafetyManager _safety;
    private readonly ZoomEngine _zoom;
    private readonly SequenceExecutor _executor;
    private readonly PerformanceMonitor _perf;
    private readonly DispatcherTimer _statsTimer;

    public DashboardView(
        ConfigurationManager config,
        SafetyManager safety,
        ZoomEngine zoom,
        SequenceExecutor executor,
        PerformanceMonitor perf)
    {
        InitializeComponent();
        _config = config;
        _safety = safety;
        _zoom = zoom;
        _executor = executor;
        _perf = perf;

        _zoom.StateChanged += () => Dispatcher.Invoke(RefreshView);
        _safety.SequenceActivityChanged += (_, _) => Dispatcher.Invoke(RefreshView);

        _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statsTimer.Tick += (_, _) => UpdatePerfStats();

        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                RefreshView();
                UpdatePerfStats();
                _statsTimer.Start();
            }
            else
            {
                _statsTimer.Stop();
            }
        };
    }

    public void RefreshView()
    {
        ZoomStatusText.Text = _zoom.IsZoomActive ? "Active" : (_config.Settings.Zoom.Enabled ? "Idle" : "Disabled");
        ZoomMultiplierText.Text = $"{(_zoom.IsZoomActive ? _zoom.TargetMultiplier : 1.0):0.0}x • {_config.Settings.Zoom.PositionMode}";

        KeyboardStatusText.Text = _safety.IsSequenceRunning
            ? "Running"
            : (_config.Settings.KeyboardModuleEnabled ? "Ready" : "Disabled");

        var seqCount = _config.ActiveProfile.Sequences.Count;
        SequenceCountText.Text = $"{seqCount} sequence(s) in profile";
        ActiveProfileText.Text = _config.ActiveProfile.Name;
        StatusDetailText.Text = _zoom.StatusMessage;

        ShortcutsList.ItemsSource = _config.ActiveProfile.Hotkeys
            .Where(h => h.Enabled)
            .ToList();
    }

    private void UpdatePerfStats()
    {
        _perf.Sample();
        CpuText.Text = $"CPU: {_perf.CpuPercent:0.0}%";
        MemoryText.Text = $"RAM: {_perf.MemoryMb:0.0} MB";
    }

    private void OnToggleZoomClick(object sender, RoutedEventArgs e) => _zoom.Toggle();
    private void OnIncreaseZoomClick(object sender, RoutedEventArgs e) => _zoom.IncreaseZoom();
    private void OnDecreaseZoomClick(object sender, RoutedEventArgs e) => _zoom.DecreaseZoom();
    private void OnResetZoomClick(object sender, RoutedEventArgs e) => _zoom.ResetZoom();
    private void OnEmergencyStopClick(object sender, RoutedEventArgs e) => _safety.TriggerEmergencyStop("Dashboard Emergency Stop");
}
