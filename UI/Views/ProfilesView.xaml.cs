using System.Windows;
using System.Windows.Controls;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Engines.Zoom;
using ScreenZoomUtility.Models;

namespace ScreenZoomUtility.UI.Views;

public partial class ProfilesView : UserControl
{
    private readonly ConfigurationManager _config;
    private readonly ZoomEngine _zoom;
    private readonly Action _onProfileChanged;
    private UtilityProfile? _selected;

    public ProfilesView(ConfigurationManager config, ZoomEngine zoom, Action onProfileChanged)
    {
        InitializeComponent();
        _config = config;
        _zoom = zoom;
        _onProfileChanged = onProfileChanged;
        RefreshView();
    }

    public void RefreshView()
    {
        var currentId = _selected?.Id ?? _config.ActiveProfile.Id;
        ProfilesList.ItemsSource = null;
        ProfilesList.ItemsSource = _config.Profiles;
        ProfilesList.SelectedItem = _config.Profiles.FirstOrDefault(p => p.Id == currentId) ?? _config.Profiles.FirstOrDefault();
    }

    private void OnProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        _selected = ProfilesList.SelectedItem as UtilityProfile;
        if (_selected == null) return;

        ProfileNameBox.Text = _selected.Name;
        ProfileDescBox.Text = _selected.Description;
        var isActive = _selected.Id == _config.Settings.ActiveProfileId;
        ProfileStatsText.Text = $"{(isActive ? "[ACTIVE] " : "")}Default Zoom: {_selected.Zoom.DefaultMultiplier:0.0}x • Mode: {_selected.Zoom.PositionMode} • {_selected.Sequences.Count} sequence(s)";
    }

    private void OnNewProfileClick(object sender, RoutedEventArgs e)
    {
        var profile = new UtilityProfile
        {
            Name = $"Profile {_config.Profiles.Count + 1}",
            Description = "Custom accessibility & productivity profile."
        };
        _config.Profiles.Add(profile);
        _config.SaveAll();
        _selected = profile;
        RefreshView();
    }

    private void OnDuplicateProfileClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;

        var seqIdMap = new Dictionary<string, string>();
        var clonedSequences = new List<KeyboardSequence>();
        foreach (var seq in _selected.Sequences)
        {
            var cloned = seq.Clone(seq.Name);
            seqIdMap[seq.Id] = cloned.Id;
            clonedSequences.Add(cloned);
        }

        var clonedHotkeys = _selected.Hotkeys.Select(h =>
        {
            var mappedSeqId = h.TargetSequenceId != null && seqIdMap.TryGetValue(h.TargetSequenceId, out var newSeqId)
                ? newSeqId
                : h.TargetSequenceId;
            return new HotkeyBinding
            {
                Id = mappedSeqId != null ? $"seq.{mappedSeqId}" : h.Id,
                ActionName = h.ActionName,
                Category = h.Category,
                Ctrl = h.Ctrl,
                Alt = h.Alt,
                Shift = h.Shift,
                Win = h.Win,
                VirtualKey = h.VirtualKey,
                KeyDisplay = h.KeyDisplay,
                Enabled = h.Enabled,
                TargetSequenceId = mappedSeqId
            };
        }).ToList();

        var copy = new UtilityProfile
        {
            Name = $"{_selected.Name} (Copy)",
            Description = _selected.Description,
            Zoom = ConfigurationManager.CloneZoomSettings(_selected.Zoom),
            Sequences = clonedSequences,
            Hotkeys = clonedHotkeys
        };
        _config.Profiles.Add(copy);
        _config.SaveAll();
        _selected = copy;
        RefreshView();
    }

    private void OnDeleteProfileClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null || _config.Profiles.Count <= 1) return;
        var deletingActive = _selected.Id == _config.Settings.ActiveProfileId;
        _config.Profiles.Remove(_selected);
        _selected = _config.Profiles[0];
        if (deletingActive)
        {
            _config.SetActiveProfile(_selected.Id);
            _onProfileChanged();
        }
        else
        {
            _config.SaveAll();
        }
        RefreshView();
    }

    private void OnActivateProfileClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        _config.SetActiveProfile(_selected.Id);
        _onProfileChanged();
        RefreshView();
    }

    private void OnSaveProfileClick(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        _selected.Name = string.IsNullOrWhiteSpace(ProfileNameBox.Text) ? "Profile" : ProfileNameBox.Text.Trim();
        _selected.Description = ProfileDescBox.Text.Trim();
        _config.SaveAll();
        _onProfileChanged();
        RefreshView();
    }
}
