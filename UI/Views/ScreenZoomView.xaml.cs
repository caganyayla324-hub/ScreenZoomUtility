using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Engines.Zoom;
using ScreenZoomUtility.Models;

namespace ScreenZoomUtility.UI.Views;

public partial class ScreenZoomView : UserControl
{
    private readonly ConfigurationManager _config;
    private readonly ZoomEngine _zoom;
    private bool _loading;

    public ScreenZoomView(ConfigurationManager config, ZoomEngine zoom)
    {
        InitializeComponent();
        _config = config;
        _zoom = zoom;

        _zoom.StateChanged += () => Dispatcher.Invoke(() =>
        {
            LiveStatusText.Text = _zoom.StatusMessage;
        });

        BuildPresetButtons();
        RefreshView();
    }

    private void BuildPresetButtons()
    {
        PresetButtonsPanel.Children.Clear();
        foreach (var preset in _config.Settings.Zoom.Presets)
        {
            var btn = new Button
            {
                Content = $"{preset:0.0}x",
                Style = (Style)FindResource("SecondaryButtonStyle"),
                Margin = new Thickness(0, 0, 8, 8),
                MinWidth = 62,
                Tag = preset
            };
            btn.Click += (_, _) =>
            {
                CustomZoomBox.Text = preset.ToString("0.0", CultureInfo.InvariantCulture);
                _zoom.SetMultiplier(preset);
                SyncProfileZoom();
            };
            PresetButtonsPanel.Children.Add(btn);
        }
    }

    public void RefreshView()
    {
        _loading = true;
        var z = _config.Settings.Zoom;
        LiveStatusText.Text = _zoom.StatusMessage;
        CustomZoomBox.Text = z.DefaultMultiplier.ToString("0.0", CultureInfo.InvariantCulture);
        ManualXBox.Text = z.ManualX.ToString();
        ManualYBox.Text = z.ManualY.ToString();
        SmoothingSlider.Value = z.SmoothingFactor;
        SmoothingValueText.Text = z.SmoothingFactor.ToString("0.00", CultureInfo.InvariantCulture);
        ClampEdgesCheck.IsChecked = z.ClampToScreenEdges;
        ZoomEnabledCheck.IsChecked = z.Enabled;

        SelectComboByTag(PositionModeCombo, z.PositionMode.ToString());
        SelectComboByTag(QualityCombo, z.ScalingQuality.ToString());
        ManualCoordsPanel.Visibility = z.PositionMode == ZoomPositionMode.ManualCoordinates
            ? Visibility.Visible
            : Visibility.Collapsed;

        _loading = false;
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                break;
            }
        }
    }

    private void OnActivateClick(object sender, RoutedEventArgs e) => _zoom.Activate();
    private void OnDeactivateClick(object sender, RoutedEventArgs e) => _zoom.Deactivate();
    private void OnResetClick(object sender, RoutedEventArgs e) => _zoom.EmergencyReset();

    private void OnApplyCustomClick(object sender, RoutedEventArgs e)
    {
        CustomValidationText.Text = string.Empty;
        var normalized = CustomZoomBox.Text.Trim().Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
        {
            CustomValidationText.Text = "Enter a valid number (e.g. 2.5).";
            return;
        }

        var z = _config.Settings.Zoom;
        if (val < z.MinMultiplier || val > z.MaxMultiplier)
        {
            CustomValidationText.Text = $"Must be between {z.MinMultiplier:0.0}x and {z.MaxMultiplier:0.0}x.";
            return;
        }

        _zoom.SetMultiplier(val);
        SyncProfileZoom();
    }

    private void OnApplyCoordsClick(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(ManualXBox.Text, out var x) && int.TryParse(ManualYBox.Text, out var y))
        {
            _config.Settings.Zoom.ManualX = Math.Max(0, x);
            _config.Settings.Zoom.ManualY = Math.Max(0, y);
            SyncProfileZoom();
        }
    }

    private void OnSmoothingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _config.Settings.Zoom.SmoothingFactor = Math.Round(e.NewValue, 2);
        SmoothingValueText.Text = _config.Settings.Zoom.SmoothingFactor.ToString("0.00", CultureInfo.InvariantCulture);
        SyncProfileZoom();
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        if (PositionModeCombo.SelectedItem is ComboBoxItem posItem &&
            Enum.TryParse<ZoomPositionMode>(posItem.Tag?.ToString(), out var mode))
        {
            _config.Settings.Zoom.PositionMode = mode;
            ManualCoordsPanel.Visibility = mode == ZoomPositionMode.ManualCoordinates
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (QualityCombo.SelectedItem is ComboBoxItem qItem &&
            Enum.TryParse<ScalingQualityMode>(qItem.Tag?.ToString(), out var quality))
        {
            _config.Settings.Zoom.ScalingQuality = quality;
        }

        _config.Settings.Zoom.ClampToScreenEdges = ClampEdgesCheck.IsChecked == true;
        _config.Settings.Zoom.Enabled = ZoomEnabledCheck.IsChecked == true;

        SyncProfileZoom();
        _zoom.ApplyProfileSettings(_config.Settings.Zoom);
    }

    private void SyncProfileZoom()
    {
        _config.ActiveProfile.Zoom.DefaultMultiplier = _config.Settings.Zoom.DefaultMultiplier;
        _config.ActiveProfile.Zoom.PositionMode = _config.Settings.Zoom.PositionMode;
        _config.ActiveProfile.Zoom.ScalingQuality = _config.Settings.Zoom.ScalingQuality;
        _config.ActiveProfile.Zoom.SmoothingFactor = _config.Settings.Zoom.SmoothingFactor;
        _config.ActiveProfile.Zoom.ManualX = _config.Settings.Zoom.ManualX;
        _config.ActiveProfile.Zoom.ManualY = _config.Settings.Zoom.ManualY;
        _config.ActiveProfile.Zoom.ClampToScreenEdges = _config.Settings.Zoom.ClampToScreenEdges;
        _config.ActiveProfile.Zoom.Enabled = _config.Settings.Zoom.Enabled;
        _config.SaveAll();
    }
}
