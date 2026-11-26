using System.IO;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Models;
using Xunit;

namespace ScreenZoomUtility.Tests;

public sealed class ConfigurationManagerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "SZU_Tests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void LoadAll_CreatesDefaultsAndSequenceHotkeys()
    {
        var cfg = new ConfigurationManager(_tempDir);
        cfg.LoadAll();

        Assert.True(File.Exists(cfg.SettingsPath));
        Assert.True(File.Exists(cfg.ProfilesPath));
        Assert.True(cfg.Profiles.Count >= 2);
        Assert.Equal("default", cfg.ActiveProfile.Id);

        // Verify the default sample sequence automatically has a hotkey entry
        Assert.Contains(cfg.ActiveProfile.Hotkeys, h => h.TargetSequenceId == "seq-sample-copy");
    }

    [Fact]
    public void SaveAndReload_PersistsCustomProfileAndZoomSettings()
    {
        var cfg = new ConfigurationManager(_tempDir);
        cfg.LoadAll();

        cfg.Settings.Zoom.MaxMultiplier = 12.0;
        cfg.Profiles.Add(new UtilityProfile { Id = "custom-1", Name = "Custom QA Profile" });
        cfg.SaveAll();

        var reloaded = new ConfigurationManager(_tempDir);
        reloaded.LoadAll();

        Assert.Equal(12.0, reloaded.Settings.Zoom.MaxMultiplier);
        Assert.Contains(reloaded.Profiles, p => p.Id == "custom-1" && p.Name == "Custom QA Profile");
    }

    [Fact]
    public void LoadAll_RecoversGracefullyFromCorruptJson()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "Settings.json"), "{ not valid json !!!");
        File.WriteAllText(Path.Combine(_tempDir, "Profiles.json"), "[ broken");

        var cfg = new ConfigurationManager(_tempDir);
        cfg.LoadAll();

        Assert.NotNull(cfg.Settings);
        Assert.True(cfg.Profiles.Count >= 2);
        Assert.NotNull(cfg.ActiveProfile);
    }

    [Fact]
    public void SwitchToNextProfile_CyclesProfilesCleanly()
    {
        var cfg = new ConfigurationManager(_tempDir);
        cfg.LoadAll();

        var firstId = cfg.ActiveProfile.Id;
        var next = cfg.SwitchToNextProfile();
        Assert.NotEqual(firstId, next.Id);

        var wrapped = cfg.SwitchToNextProfile();
        Assert.Equal(firstId, wrapped.Id);
    }
}
