using System.Windows;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Engines.Keyboard;
using ScreenZoomUtility.Engines.Zoom;
using ScreenZoomUtility.UI;

namespace ScreenZoomUtility;

public partial class App : Application
{
    private ConfigurationManager? _configManager;
    private SafetyManager? _safetyManager;
    private ZoomEngine? _zoomEngine;
    private HotkeyManager? _hotkeyManager;
    private SequenceExecutor? _sequenceExecutor;
    private PerformanceMonitor? _performanceMonitor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            SafeEmergencyCleanup();
            MessageBox.Show(
                $"An unexpected error occurred and all active zoom/keyboard operations were safely halted.\n\n{args.Exception.Message}",
                "Utility Safety Recovery",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, _) => SafeEmergencyCleanup();

        _configManager = new ConfigurationManager();
        _configManager.LoadAll();

        _safetyManager = new SafetyManager(_configManager);
        _zoomEngine = new ZoomEngine(_configManager, _safetyManager);
        _hotkeyManager = new HotkeyManager();
        _sequenceExecutor = new SequenceExecutor(_safetyManager);
        _performanceMonitor = new PerformanceMonitor();

        var mainWindow = new MainWindow(
            _configManager,
            _safetyManager,
            _zoomEngine,
            _hotkeyManager,
            _sequenceExecutor,
            _performanceMonitor);

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SafeEmergencyCleanup();
        _hotkeyManager?.Dispose();
        _zoomEngine?.Dispose();
        _sequenceExecutor?.Dispose();
        _performanceMonitor?.Dispose();
        _configManager?.SaveAll();
        base.OnExit(e);
    }

    private void SafeEmergencyCleanup()
    {
        try { _safetyManager?.TriggerEmergencyStop("Application cleanup"); } catch { }
        try { _zoomEngine?.EmergencyReset(); } catch { }
        try { _sequenceExecutor?.CancelCurrent(); } catch { }
    }
}
