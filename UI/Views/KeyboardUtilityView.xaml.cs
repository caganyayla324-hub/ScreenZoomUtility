using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Engines.Keyboard;
using ScreenZoomUtility.Models;

namespace ScreenZoomUtility.UI.Views;

public partial class KeyboardUtilityView : UserControl
{
    private readonly ConfigurationManager _config;
    private readonly SafetyManager _safety;
    private readonly SequenceExecutor _executor;
    private readonly Action _onSequencesChanged;

    private KeyboardSequence? _selectedSequence;
    private SequenceStep? _selectedStep;
    private int _capturedVk;
    private string _capturedKeyDisplay = string.Empty;

    public KeyboardUtilityView(
        ConfigurationManager config,
        SafetyManager safety,
        SequenceExecutor executor,
        Action onSequencesChanged)
    {
        InitializeComponent();
        _config = config;
        _safety = safety;
        _executor = executor;
        _onSequencesChanged = onSequencesChanged;

        _executor.ExecutionLog += msg => Dispatcher.Invoke(() =>
        {
            ExecutionLogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            ExecutionLogBox.ScrollToEnd();
        });

        RefreshView();
    }

    public void RefreshView()
    {
        var prevId = _selectedSequence?.Id;
        SequencesList.ItemsSource = null;
        SequencesList.ItemsSource = _config.ActiveProfile.Sequences;

        var toSelect = _config.ActiveProfile.Sequences.FirstOrDefault(s => s.Id == prevId)
                       ?? _config.ActiveProfile.Sequences.FirstOrDefault();

        SequencesList.SelectedItem = toSelect;
        LoadSelectedSequence();
    }

    private void OnSequenceSelected(object sender, SelectionChangedEventArgs e)
    {
        _selectedSequence = SequencesList.SelectedItem as KeyboardSequence;
        LoadSelectedSequence();
    }

    private void LoadSelectedSequence()
    {
        if (_selectedSequence == null)
        {
            SequenceNameBox.Text = string.Empty;
            StepsList.ItemsSource = null;
            TimelineSummaryText.Text = "Select or create a sequence.";
            return;
        }

        SequenceNameBox.Text = _selectedSequence.Name;
        RefreshStepsList();
    }

    private void RefreshStepsList(int? selectIndex = null)
    {
        if (_selectedSequence == null) return;
        StepsList.ItemsSource = null;
        StepsList.ItemsSource = _selectedSequence.Steps;

        var total = _selectedSequence.TotalDurationMs;
        var max = _config.Settings.Safety.MaxSequenceDurationMs;
        TimelineSummaryText.Text = $"Steps: {_selectedSequence.Steps.Count} • Total Delay: {total} ms (Limit: {max} ms)";

        if (selectIndex.HasValue && selectIndex.Value >= 0 && selectIndex.Value < _selectedSequence.Steps.Count)
            StepsList.SelectedIndex = selectIndex.Value;
        else if (_selectedSequence.Steps.Count > 0 && StepsList.SelectedIndex < 0)
            StepsList.SelectedIndex = 0;
    }

    private void OnStepSelected(object sender, SelectionChangedEventArgs e)
    {
        _selectedStep = StepsList.SelectedItem as SequenceStep;
        StepErrorText.Text = string.Empty;
        if (_selectedStep == null) return;

        StepLabelBox.Text = _selectedStep.Label;
        StepCtrlCheck.IsChecked = _selectedStep.Ctrl;
        StepAltCheck.IsChecked = _selectedStep.Alt;
        StepShiftCheck.IsChecked = _selectedStep.Shift;
        StepWinCheck.IsChecked = _selectedStep.Win;
        StepDelayBox.Text = _selectedStep.DelayAfterMs.ToString();
        _capturedVk = _selectedStep.VirtualKey;
        _capturedKeyDisplay = _selectedStep.KeyDisplay;
        StepKeyCaptureBox.Text = _selectedStep.KeyDisplay;
    }

    private void OnStepKeyCapture(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (KeyMapper.IsModifierKey(key)) return;

        _capturedVk = KeyMapper.ToVirtualKey(key);
        _capturedKeyDisplay = KeyMapper.ToDisplayName(key);
        StepKeyCaptureBox.Text = _capturedKeyDisplay;
    }

    private void OnPresetDelayClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string ms)
        {
            StepDelayBox.Text = ms;
        }
    }

    private void OnApplyStepClick(object sender, RoutedEventArgs e)
    {
        if (_selectedStep == null) return;
        StepErrorText.Text = string.Empty;

        if (!int.TryParse(StepDelayBox.Text, out var delayMs))
        {
            StepErrorText.Text = "Delay must be a valid integer in milliseconds.";
            return;
        }

        var (ok, err) = _safety.ValidateDelay(delayMs);
        if (!ok)
        {
            StepErrorText.Text = err;
            return;
        }

        _selectedStep.Label = string.IsNullOrWhiteSpace(StepLabelBox.Text) ? "Key Step" : StepLabelBox.Text.Trim();
        _selectedStep.Ctrl = StepCtrlCheck.IsChecked == true;
        _selectedStep.Alt = StepAltCheck.IsChecked == true;
        _selectedStep.Shift = StepShiftCheck.IsChecked == true;
        _selectedStep.Win = StepWinCheck.IsChecked == true;
        _selectedStep.DelayAfterMs = delayMs;
        if (_capturedVk > 0)
        {
            _selectedStep.VirtualKey = _capturedVk;
            _selectedStep.KeyDisplay = _capturedKeyDisplay;
        }

        var idx = StepsList.SelectedIndex;
        _config.SaveAll();
        RefreshStepsList(idx);
        RefreshSequenceListDisplay();
    }

    private void OnAddStepClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSequence == null) return;
        if (_selectedSequence.Steps.Count >= _config.Settings.Safety.MaxStepsPerSequence)
        {
            StepErrorText.Text = $"Maximum steps per sequence ({_config.Settings.Safety.MaxStepsPerSequence}) reached.";
            return;
        }

        var step = new SequenceStep
        {
            Label = $"Step {_selectedSequence.Steps.Count + 1}",
            VirtualKey = 0x41,
            KeyDisplay = "A",
            DelayAfterMs = 100
        };
        _selectedSequence.Steps.Add(step);
        _config.SaveAll();
        RefreshStepsList(_selectedSequence.Steps.Count - 1);
        RefreshSequenceListDisplay();
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSequence == null) return;
        var idx = StepsList.SelectedIndex;
        if (idx <= 0) return;
        (_selectedSequence.Steps[idx - 1], _selectedSequence.Steps[idx]) = (_selectedSequence.Steps[idx], _selectedSequence.Steps[idx - 1]);
        _config.SaveAll();
        RefreshStepsList(idx - 1);
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSequence == null) return;
        var idx = StepsList.SelectedIndex;
        if (idx < 0 || idx >= _selectedSequence.Steps.Count - 1) return;
        (_selectedSequence.Steps[idx + 1], _selectedSequence.Steps[idx]) = (_selectedSequence.Steps[idx], _selectedSequence.Steps[idx + 1]);
        _config.SaveAll();
        RefreshStepsList(idx + 1);
    }

    private void OnRemoveStepClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSequence == null) return;
        var idx = StepsList.SelectedIndex;
        if (idx < 0) return;
        _selectedSequence.Steps.RemoveAt(idx);
        _config.SaveAll();
        RefreshStepsList(Math.Max(0, idx - 1));
        RefreshSequenceListDisplay();
    }

    private void OnNewSequenceClick(object sender, RoutedEventArgs e)
    {
        var seq = new KeyboardSequence
        {
            Name = $"Sequence {_config.ActiveProfile.Sequences.Count + 1}",
            Steps =
            [
                new SequenceStep { Label = "Step 1", Ctrl = true, VirtualKey = 0x43, KeyDisplay = "C", DelayAfterMs = 150 }
            ]
        };
        _config.ActiveProfile.Sequences.Add(seq);
        EnsureSequenceHotkey(seq);
        _config.SaveAll();
        _selectedSequence = seq;
        RefreshView();
        _onSequencesChanged();
    }

    private void OnDuplicateSequenceClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSequence == null) return;
        var copy = _selectedSequence.Clone($"{_selectedSequence.Name} (Copy)");
        _config.ActiveProfile.Sequences.Add(copy);
        EnsureSequenceHotkey(copy);
        _config.SaveAll();
        _selectedSequence = copy;
        RefreshView();
        _onSequencesChanged();
    }

    private void OnDeleteSequenceClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSequence == null) return;
        var id = _selectedSequence.Id;
        _config.ActiveProfile.Sequences.Remove(_selectedSequence);
        _config.ActiveProfile.Hotkeys.RemoveAll(h => h.TargetSequenceId == id);
        _config.SaveAll();
        _selectedSequence = null;
        RefreshView();
        _onSequencesChanged();
    }

    private void OnSaveSequenceClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSequence == null) return;
        _selectedSequence.Name = string.IsNullOrWhiteSpace(SequenceNameBox.Text) ? "Unnamed Sequence" : SequenceNameBox.Text.Trim();
        var hk = _config.ActiveProfile.Hotkeys.FirstOrDefault(h => h.TargetSequenceId == _selectedSequence.Id);
        if (hk != null) hk.ActionName = $"Run Sequence: {_selectedSequence.Name}";
        _config.SaveAll();
        RefreshSequenceListDisplay();
        _onSequencesChanged();
    }

    private async void OnRunSequenceClick(object sender, RoutedEventArgs e)
    {
        if (_selectedSequence == null) return;
        await _executor.ExecuteAsync(_selectedSequence);
    }

    private void OnStopSequenceClick(object sender, RoutedEventArgs e)
    {
        _executor.CancelCurrent();
    }

    private void OnClearLogClick(object sender, RoutedEventArgs e)
    {
        ExecutionLogBox.Clear();
    }

    private void RefreshSequenceListDisplay()
    {
        var current = _selectedSequence;
        SequencesList.ItemsSource = null;
        SequencesList.ItemsSource = _config.ActiveProfile.Sequences;
        SequencesList.SelectedItem = current;
    }

    private void EnsureSequenceHotkey(KeyboardSequence seq)
    {
        if (_config.ActiveProfile.Hotkeys.Any(h => h.TargetSequenceId == seq.Id)) return;
        _config.ActiveProfile.Hotkeys.Add(new HotkeyBinding
        {
            Id = $"seq.{seq.Id}",
            ActionName = $"Run Sequence: {seq.Name}",
            Category = "Keyboard Sequence",
            Enabled = false,
            VirtualKey = 0,
            KeyDisplay = string.Empty,
            TargetSequenceId = seq.Id
        });
    }
}
