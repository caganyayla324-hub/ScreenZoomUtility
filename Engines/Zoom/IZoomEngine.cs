using ScreenZoomUtility.Models;

namespace ScreenZoomUtility.Engines.Zoom;

public interface IZoomEngine : IDisposable
{
    bool IsInitialized { get; }
    bool IsZoomActive { get; }
    double CurrentMultiplier { get; }
    double TargetMultiplier { get; }
    ZoomPositionMode PositionMode { get; }
    string StatusMessage { get; }

    event Action? StateChanged;

    bool Initialize();
    void Activate();
    void Deactivate();
    void Toggle();
    void SetMultiplier(double multiplier);
    void IncreaseZoom();
    void DecreaseZoom();
    void ResetZoom();
    void EmergencyReset();
    void ApplyProfileSettings(ZoomSettings settings);
}
