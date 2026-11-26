using System.Text.Json.Serialization;

namespace ScreenZoomUtility.Models;

public class SequenceStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Label { get; set; } = "Key Press";
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public int VirtualKey { get; set; } = 0x41; // 'A'
    public string KeyDisplay { get; set; } = "A";
    public int DelayAfterMs { get; set; } = 100;

    [JsonIgnore]
    public string ShortcutSummary
    {
        get
        {
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(string.IsNullOrWhiteSpace(KeyDisplay) ? $"VK_{VirtualKey:X2}" : KeyDisplay);
            return string.Join(" + ", parts);
        }
    }

    public SequenceStep Clone() => new()
    {
        Id = Guid.NewGuid().ToString("N")[..8],
        Label = Label,
        Ctrl = Ctrl,
        Alt = Alt,
        Shift = Shift,
        Win = Win,
        VirtualKey = VirtualKey,
        KeyDisplay = KeyDisplay,
        DelayAfterMs = DelayAfterMs
    };
}

public class KeyboardSequence
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "New Sequence";
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<SequenceStep> Steps { get; set; } = [];

    [JsonIgnore]
    public int TotalDurationMs => Steps.Sum(s => Math.Max(0, s.DelayAfterMs));

    [JsonIgnore]
    public string Summary => $"{Steps.Count} step(s) • {TotalDurationMs} ms total";

    public KeyboardSequence Clone(string newName) => new()
    {
        Id = Guid.NewGuid().ToString("N")[..8],
        Name = newName,
        Description = Description,
        Enabled = Enabled,
        Steps = Steps.Select(s => s.Clone()).ToList()
    };
}

public class UtilityProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Default Profile";
    public string Description { get; set; } = "Standard Windows accessibility and productivity profile.";
    public ZoomSettings Zoom { get; set; } = new();
    public List<KeyboardSequence> Sequences { get; set; } = [];
    public List<HotkeyBinding> Hotkeys { get; set; } = AppSettings.CreateDefaultHotkeys();

    public static UtilityProfile CreateDefault()
    {
        var sampleSeq = new KeyboardSequence
        {
            Id = "seq-sample-copy",
            Name = "Copy & Switch Window",
            Description = "Example productivity sequence: copies selection, waits 150ms, switches window.",
            Enabled = true,
            Steps =
            [
                new SequenceStep { Label = "Copy selection", Ctrl = true, VirtualKey = 0x43, KeyDisplay = "C", DelayAfterMs = 150 },
                new SequenceStep { Label = "Switch window", Alt = true, VirtualKey = 0x09, KeyDisplay = "Tab", DelayAfterMs = 250 }
            ]
        };

        return new UtilityProfile
        {
            Id = "default",
            Name = "Default Productivity",
            Description = "2.0x cursor-tracking magnification and general productivity shortcuts.",
            Zoom = new ZoomSettings
            {
                DefaultMultiplier = 2.0,
                PositionMode = ZoomPositionMode.CursorTracking,
                ScalingQuality = ScalingQualityMode.HighQuality
            },
            Sequences = [sampleSeq],
            Hotkeys = AppSettings.CreateDefaultHotkeys()
        };
    }

    public static UtilityProfile CreateReadingProfile() => new()
    {
        Id = "reading",
        Name = "Document Reading (3.0x)",
        Description = "Higher magnification centered on cursor for small text and UI inspection.",
        Zoom = new ZoomSettings
        {
            DefaultMultiplier = 3.0,
            PositionMode = ZoomPositionMode.CursorTracking,
            SmoothingFactor = 0.2,
            ScalingQuality = ScalingQualityMode.HighQuality
        },
        Sequences = [],
        Hotkeys = AppSettings.CreateDefaultHotkeys()
    };
}
