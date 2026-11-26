using System.Text.Json.Serialization;

namespace ScreenZoomUtility.Models;

public enum ZoomPositionMode
{
    CursorTracking,
    ScreenCenter,
    ManualCoordinates
}

public enum ScalingQualityMode
{
    Balanced,
    HighQuality,
    LowLatency
}

public class ZoomSettings
{
    public bool Enabled { get; set; } = true;
    public double DefaultMultiplier { get; set; } = 2.0;
    public double MinMultiplier { get; set; } = 1.0;
    public double MaxMultiplier { get; set; } = 8.0;
    public double StepSize { get; set; } = 0.5;
    public ZoomPositionMode PositionMode { get; set; } = ZoomPositionMode.CursorTracking;
    public ScalingQualityMode ScalingQuality { get; set; } = ScalingQualityMode.HighQuality;
    public double SmoothingFactor { get; set; } = 0.25;
    public int CursorDeadzonePx { get; set; } = 4;
    public int ManualX { get; set; } = 960;
    public int ManualY { get; set; } = 540;
    public bool ClampToScreenEdges { get; set; } = true;
    public List<double> Presets { get; set; } = [1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, 8.0];
}

public class SafetySettings
{
    public int MaxSequenceDurationMs { get; set; } = 30000;
    public int MinStepDelayMs { get; set; } = 20;
    public int MaxStepDelayMs { get; set; } = 60000;
    public int MaxStepsPerSequence { get; set; } = 50;
    public int ExecutionCooldownMs { get; set; } = 300;
    public bool PreventSimultaneousSequences { get; set; } = true;
    public bool ShowActiveIndicator { get; set; } = true;
}

public class HotkeyBinding
{
    public string Id { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public int VirtualKey { get; set; }
    public string KeyDisplay { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string? TargetSequenceId { get; set; }

    [JsonIgnore]
    public string FormattedShortcut
    {
        get
        {
            if (VirtualKey == 0) return "Unassigned";
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(string.IsNullOrWhiteSpace(KeyDisplay) ? $"VK_{VirtualKey:X2}" : KeyDisplay);
            return string.Join(" + ", parts);
        }
    }

    public string Signature => $"{Ctrl}:{Alt}:{Shift}:{Win}:{VirtualKey}";
}

public class AppSettings
{
    public string ActiveProfileId { get; set; } = "default";
    public bool StartMinimized { get; set; } = false;
    public bool ConfirmOnExit { get; set; } = false;
    public bool KeyboardModuleEnabled { get; set; } = true;
    public ZoomSettings Zoom { get; set; } = new();
    public SafetySettings Safety { get; set; } = new();
    public List<HotkeyBinding> Hotkeys { get; set; } = CreateDefaultHotkeys();

    public static List<HotkeyBinding> CreateDefaultHotkeys() =>
    [
        new HotkeyBinding
        {
            Id = "zoom.toggle",
            ActionName = "Toggle Screen Zoom",
            Category = "Screen Zoom",
            Ctrl = true, Alt = true, VirtualKey = 0x5A, KeyDisplay = "Z", Enabled = true
        },
        new HotkeyBinding
        {
            Id = "zoom.activate",
            ActionName = "Activate Screen Zoom",
            Category = "Screen Zoom",
            Ctrl = true, Alt = true, Shift = true, VirtualKey = 0x5A, KeyDisplay = "Z", Enabled = false
        },
        new HotkeyBinding
        {
            Id = "zoom.deactivate",
            ActionName = "Deactivate Screen Zoom",
            Category = "Screen Zoom",
            Ctrl = true, Alt = true, VirtualKey = 0x58, KeyDisplay = "X", Enabled = true
        },
        new HotkeyBinding
        {
            Id = "zoom.increase",
            ActionName = "Increase Magnification",
            Category = "Screen Zoom",
            Ctrl = true, Alt = true, VirtualKey = 0xBB, KeyDisplay = "Plus", Enabled = true
        },
        new HotkeyBinding
        {
            Id = "zoom.decrease",
            ActionName = "Decrease Magnification",
            Category = "Screen Zoom",
            Ctrl = true, Alt = true, VirtualKey = 0xBD, KeyDisplay = "Minus", Enabled = true
        },
        new HotkeyBinding
        {
            Id = "zoom.reset",
            ActionName = "Reset Magnification (1.0x)",
            Category = "Screen Zoom",
            Ctrl = true, Alt = true, VirtualKey = 0x30, KeyDisplay = "0", Enabled = true
        },
        new HotkeyBinding
        {
            Id = "zoom.next_profile",
            ActionName = "Switch Zoom Profile",
            Category = "Screen Zoom",
            Ctrl = true, Alt = true, VirtualKey = 0x50, KeyDisplay = "P", Enabled = true
        },
        new HotkeyBinding
        {
            Id = "safety.emergency_stop",
            ActionName = "Emergency Stop & Reset All",
            Category = "Safety",
            Ctrl = true, Alt = true, VirtualKey = 0x1B, KeyDisplay = "Esc", Enabled = true
        }
    ];
}
