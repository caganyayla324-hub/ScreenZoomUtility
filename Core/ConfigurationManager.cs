using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenZoomUtility.Models;

namespace ScreenZoomUtility.Core;

public class ConfigurationManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ConfigDirectory { get; }
    public string SettingsPath => Path.Combine(ConfigDirectory, "Settings.json");
    public string ProfilesPath => Path.Combine(ConfigDirectory, "Profiles.json");

    public AppSettings Settings { get; private set; } = new();
    public List<UtilityProfile> Profiles { get; private set; } = [];

    public event Action? ConfigurationChanged;

    public ConfigurationManager(string? configDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(configDirectory))
        {
            ConfigDirectory = configDirectory;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            ConfigDirectory = Path.Combine(appData, "ScreenZoomUtility");
        }
        Directory.CreateDirectory(ConfigDirectory);
    }

    public UtilityProfile ActiveProfile
    {
        get
        {
            var found = Profiles.FirstOrDefault(p => p.Id == Settings.ActiveProfileId);
            if (found != null) return found;
            if (Profiles.Count == 0)
            {
                Profiles.Add(UtilityProfile.CreateDefault());
                Profiles.Add(UtilityProfile.CreateReadingProfile());
            }
            Settings.ActiveProfileId = Profiles[0].Id;
            return Profiles[0];
        }
    }

    public void LoadAll()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            else
            {
                Settings = new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }

        try
        {
            if (File.Exists(ProfilesPath))
            {
                var json = File.ReadAllText(ProfilesPath);
                Profiles = JsonSerializer.Deserialize<List<UtilityProfile>>(json, JsonOptions) ?? [];
            }
        }
        catch
        {
            Profiles = [];
        }

        if (Profiles.Count == 0)
        {
            Profiles.Add(UtilityProfile.CreateDefault());
            Profiles.Add(UtilityProfile.CreateReadingProfile());
            Settings.ActiveProfileId = Profiles[0].Id;
        }

        EnsureDefaultHotkeys(Settings.Hotkeys);
        foreach (var profile in Profiles)
        {
            EnsureDefaultHotkeys(profile.Hotkeys);
            EnsureSequenceHotkeys(profile);
        }

        SaveAll();
    }

    public void SaveAll()
    {
        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, JsonOptions));
        File.WriteAllText(ProfilesPath, JsonSerializer.Serialize(Profiles, JsonOptions));
        ConfigurationChanged?.Invoke();
    }

    public void SetActiveProfile(string profileId)
    {
        var profile = Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile == null) return;
        Settings.ActiveProfileId = profile.Id;
        Settings.Zoom = CloneZoomSettings(profile.Zoom);
        SaveAll();
    }

    public UtilityProfile SwitchToNextProfile()
    {
        if (Profiles.Count <= 1) return ActiveProfile;
        var currentIndex = Profiles.FindIndex(p => p.Id == Settings.ActiveProfileId);
        var nextIndex = (currentIndex + 1) % Profiles.Count;
        SetActiveProfile(Profiles[nextIndex].Id);
        return Profiles[nextIndex];
    }

    public void ResetToDefaults()
    {
        Settings = new AppSettings();
        Profiles = [UtilityProfile.CreateDefault(), UtilityProfile.CreateReadingProfile()];
        Settings.ActiveProfileId = Profiles[0].Id;
        SaveAll();
    }

    private static void EnsureDefaultHotkeys(List<HotkeyBinding> list)
    {
        foreach (var def in AppSettings.CreateDefaultHotkeys())
        {
            if (list.All(h => h.Id != def.Id))
            {
                list.Add(def);
            }
        }
    }

    private static void EnsureSequenceHotkeys(UtilityProfile profile)
    {
        foreach (var seq in profile.Sequences)
        {
            if (profile.Hotkeys.All(h => h.TargetSequenceId != seq.Id))
            {
                profile.Hotkeys.Add(new HotkeyBinding
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
    }

    public static ZoomSettings CloneZoomSettings(ZoomSettings src) => new()
    {
        Enabled = src.Enabled,
        DefaultMultiplier = src.DefaultMultiplier,
        MinMultiplier = src.MinMultiplier,
        MaxMultiplier = src.MaxMultiplier,
        StepSize = src.StepSize,
        PositionMode = src.PositionMode,
        ScalingQuality = src.ScalingQuality,
        SmoothingFactor = src.SmoothingFactor,
        CursorDeadzonePx = src.CursorDeadzonePx,
        ManualX = src.ManualX,
        ManualY = src.ManualY,
        ClampToScreenEdges = src.ClampToScreenEdges,
        Presets = [.. src.Presets]
    };
}
