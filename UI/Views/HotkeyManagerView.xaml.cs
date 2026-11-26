using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Engines.Keyboard;
using ScreenZoomUtility.Models;

namespace ScreenZoomUtility.UI.Views;

public partial class HotkeyManagerView : UserControl
{
    private readonly ConfigurationManager _config;
    private readonly HotkeyManager _hotkeyManager;
    private readonly Action _onHotkeysChanged;

    private HotkeyBinding? _selected;
    private int _capturedVk;
    private string _capturedDisplay = string.Empty;

    public HotkeyManagerView(ConfigurationManager config, HotkeyManager hotkeyManager, Action onHotkeysChanged)
    {
        InitializeComponent();
        _config = config;
        _hotkeyManager = hotkeyManager;
        _onHotkeysChanged = onHotkeysChanged;
        RefreshView();
    }

    public void RefreshView()
    {
        var prevId = _selected?.Id;
        var list = _config.ActiveProfile.Hotkeys;

        HotkeysList.ItemsSource = null;
        HotkeysList.ItemsSource = list;
        HotkeysList.SelectedItem = list.FirstOrDefault(h => h.Id == prevId) ?? list.FirstOrDefault();

        var warnings = new List<string>();
        warnings.AddRange(_hotkeyManager.DetectConflicts(list));
        warnings.AddRange(_hotkeyManager.RegistrationErrors.Values);

        if (warnings.Count > 0)
        {
            ConflictBanner.Visibility = Visibility.Visible;
            ConflictDetailsText.Text = string.Join("\n", warnings);
        }
        else
        {
            ConflictBanner.Visibility = Visibility.Collapsed;
            ConflictDetailsText.Text = string.Empty;
        }
    }

    private void OnHotkeySelected(object sender, SelectionChangedEventArgs e)
    {
        _selected = HotkeysList.SelectedItem as HotkeyBinding;
        if (_selected == null) return;

        HkEnabledCheck.IsChecked = _selected.Enabled;
        HkCtrlCheck.IsChecked = _selected.Ctrl;
        HkAltCheck.IsChecked = _selected.Alt;
        HkShiftCheck.IsChecked = _selected.Shift;
        HkWinCheck.IsChecked = _selected.Win;
        _capturedVk = _selected.VirtualKey;
        _capturedDisplay = _selected.KeyDisplay;
        HkKeyCaptureBox.Text = string.IsNullOrWhiteSpace(_selected.KeyDisplay) ? "Press key..." : _selected.KeyDisplay;
    }

    private void OnCaptureKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (KeyMapper.IsModifierKey(key)) return;

        _capturedVk = KeyMapper.ToVirtualKey(key);
        _capturedDisplay = KeyMapper.ToDisplayName(key);
        HkKeyCaptureBox.Text = _capturedDisplay;
    }

    private void OnApplyHotkeyClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        _selected.Enabled = HkEnabledCheck.IsChecked == true;
        _selected.Ctrl = HkCtrlCheck.IsChecked == true;
        _selected.Alt = HkAltCheck.IsChecked == true;
        _selected.Shift = HkShiftCheck.IsChecked == true;
        _selected.Win = HkWinCheck.IsChecked == true;
        _selected.VirtualKey = _capturedVk;
        _selected.KeyDisplay = _capturedDisplay;

        _config.SaveAll();
        _onHotkeysChanged();
        RefreshView();
    }

    private void OnClearHotkeyClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        _selected.Enabled = false;
        _selected.Ctrl = _selected.Alt = _selected.Shift = _selected.Win = false;
        _selected.VirtualKey = 0;
        _selected.KeyDisplay = string.Empty;
        _config.SaveAll();
        _onHotkeysChanged();
        RefreshView();
    }

    private void OnResetDefaultsClick(object sender, RoutedEventArgs e)
    {
        var seqHotkeys = _config.ActiveProfile.Hotkeys.Where(h => !string.IsNullOrEmpty(h.TargetSequenceId)).ToList();
        _config.ActiveProfile.Hotkeys = AppSettings.CreateDefaultHotkeys();
        _config.ActiveProfile.Hotkeys.AddRange(seqHotkeys);
        _config.SaveAll();
        _onHotkeysChanged();
        RefreshView();
    }
}
