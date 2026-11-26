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
        var copy = new UtilityProfile
        {
            Name = $"{_selected.Name} (Copy)",
            Description = _selected.Description,
            Zoom = new ZoomSettings
            {
                Enabled = _selected.Zoom.Enabled,
                DefaultMultiplier = _selected.Zoom.DefaultMultiplier,
                MinMultiplier = _selected.Zoom.MinMultiplier,
                MaxMultiplier = _selected.Zoom.MaxMultiplier,
                StepSize = _selected.Zoom.StepSize,
                PositionMode = _selected.Zoom.PositionMode,
                ScalingQuality = _selected.Zoom.ScalingQuality,
                SmoothingFactor = _selected.Zoom.SmoothingFactor,
                ManualX = _selected.Zoom.ManualX,
                ManualY = _selected.Zoom.ManualY,
                ClampToScreenEdges = _selected.Zoom.ClampToScreenEdges
            },
            Sequences = _selected.Sequences.Select(s => s.Clone(s.Name)).ToList(),
            Hotkeys = AppSettings.CreateDefaultHotkeys()
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
