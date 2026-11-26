using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Engines.Zoom;

namespace ScreenZoomUtility.UI.Views;

public partial class SettingsView : UserControl
{
    private readonly ConfigurationManager _config;
    private readonly ZoomEngine _zoom;
    private readonly Action _onSettingsChanged;

    public SettingsView(ConfigurationManager config, ZoomEngine zoom, Action onSettingsChanged)
    {
        InitializeComponent();
        _config = config;
        _zoom = zoom;
        _onSettingsChanged = onSettingsChanged;
        RefreshView();
    }

    public void RefreshView()
    {
        var s = _config.Settings;
        MaxDurationBox.Text = s.Safety.MaxSequenceDurationMs.ToString();
        MinDelayBox.Text = s.Safety.MinStepDelayMs.ToString();
        MaxDelayBox.Text = s.Safety.MaxStepDelayMs.ToString();
        CooldownBox.Text = s.Safety.ExecutionCooldownMs.ToString();
        MaxZoomBox.Text = s.Zoom.MaxMultiplier.ToString("0.0", CultureInfo.InvariantCulture);

        KeyboardEnabledCheck.IsChecked = s.KeyboardModuleEnabled;
        PreventSimultaneousCheck.IsChecked = s.Safety.PreventSimultaneousSequences;
        ShowIndicatorCheck.IsChecked = s.Safety.ShowActiveIndicator;
        ConfirmExitCheck.IsChecked = s.ConfirmOnExit;

        ConfigPathText.Text = _config.ConfigDirectory;
        SaveFeedbackText.Text = string.Empty;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(MaxDurationBox.Text, out var maxDur))
            _config.Settings.Safety.MaxSequenceDurationMs = Math.Clamp(maxDur, 500, 300000);

        if (int.TryParse(MinDelayBox.Text, out var minDel))
            _config.Settings.Safety.MinStepDelayMs = Math.Clamp(minDel, 0, 5000);

        if (int.TryParse(MaxDelayBox.Text, out var maxDel))
            _config.Settings.Safety.MaxStepDelayMs = Math.Clamp(maxDel, _config.Settings.Safety.MinStepDelayMs, 300000);

        if (int.TryParse(CooldownBox.Text, out var cd))
            _config.Settings.Safety.ExecutionCooldownMs = Math.Clamp(cd, 0, 10000);

        var normalizedMaxZoom = MaxZoomBox.Text.Trim().Replace(',', '.');
        if (double.TryParse(normalizedMaxZoom, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxZoom))
        {
            var clamped = Math.Clamp(maxZoom, 2.0, 16.0);
            _config.Settings.Zoom.MaxMultiplier = clamped;
            _config.ActiveProfile.Zoom.MaxMultiplier = clamped;
        }

        _config.Settings.KeyboardModuleEnabled = KeyboardEnabledCheck.IsChecked == true;
        _config.Settings.Safety.PreventSimultaneousSequences = PreventSimultaneousCheck.IsChecked == true;
        _config.Settings.Safety.ShowActiveIndicator = ShowIndicatorCheck.IsChecked == true;
        _config.Settings.ConfirmOnExit = ConfirmExitCheck.IsChecked == true;

        _config.SaveAll();
        _onSettingsChanged();
        SaveFeedbackText.Text = "Settings saved.";
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", _config.ConfigDirectory) { UseShellExecute = true });
    }

    private void OnResetAllClick(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show(
            "Reset all settings, profiles, and hotkeys to factory defaults?",
            "Confirm Reset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (res != MessageBoxResult.Yes) return;

        _zoom.EmergencyReset();
        _config.ResetToDefaults();
        _onSettingsChanged();
        RefreshView();
        SaveFeedbackText.Text = "Defaults restored.";
    }
}
