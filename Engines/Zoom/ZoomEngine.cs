using System.Windows.Threading;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Models;

namespace ScreenZoomUtility.Engines.Zoom;

/// <summary>
/// Full-screen desktop magnification engine built on the documented Windows Magnification API
/// (Magnification.dll: MagInitialize / MagSetFullscreenTransform).
/// Operates exclusively at the Windows desktop compositor level without interacting with any application or game.
/// </summary>
public sealed class ZoomEngine : IZoomEngine
{
    private readonly ConfigurationManager _config;
    private readonly SafetyManager _safety;
    private readonly DispatcherTimer _updateTimer;

    private bool _isInitialized;
    private bool _isZoomActive;
    private double _currentMultiplier = 1.0;
    private double _targetMultiplier = 1.0;
    private double _currentCenterX;
    private double _currentCenterY;
    private int _lastCursorX;
    private int _lastCursorY;
    private float _lastAppliedMag = 1.0f;
    private int _lastAppliedX;
    private int _lastAppliedY;
    private bool _disposed;

    public bool IsInitialized => _isInitialized;
    public bool IsZoomActive => _isZoomActive;
    public double CurrentMultiplier => _currentMultiplier;
    public double TargetMultiplier => _targetMultiplier;
    public ZoomPositionMode PositionMode => _config.Settings.Zoom.PositionMode;
    public string StatusMessage { get; private set; } = "Idle (1.0x)";

    public event Action? StateChanged;

    public ZoomEngine(ConfigurationManager config, SafetyManager safety)
    {
        _config = config;
        _safety = safety;
        _safety.EmergencyStopTriggered += _ => EmergencyReset();

        // Timer only runs while zoom is active or animating back to 1.0x -> zero idle CPU when inactive.
        _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _updateTimer.Tick += OnTick;
    }

    public bool Initialize()
    {
        if (_isInitialized) return true;
        try
        {
            _isInitialized = NativeMethods.MagInitialize();
            StatusMessage = _isInitialized
                ? "Ready (Windows Magnification API)"
                : "Failed to initialize Windows Magnification API";
            StateChanged?.Invoke();
            return _isInitialized;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Magnification API error: {ex.Message}";
            _isInitialized = false;
            StateChanged?.Invoke();
            return false;
        }
    }

    public void Activate()
    {
        if (!_config.Settings.Zoom.Enabled)
        {
            StatusMessage = "Zoom module is disabled";
            StateChanged?.Invoke();
            return;
        }

        if (!_isInitialized && !Initialize())
            return;

        var target = Math.Clamp(
            _config.Settings.Zoom.DefaultMultiplier,
            _config.Settings.Zoom.MinMultiplier,
            _config.Settings.Zoom.MaxMultiplier);

        if (target <= 1.01) target = 2.0;

        _targetMultiplier = target;
        _isZoomActive = true;
        InitializeCenterPoint();
        ConfigureTimerInterval();

        if (!_updateTimer.IsEnabled)
            _updateTimer.Start();

        StatusMessage = $"Active ({_targetMultiplier:0.0}x • {PositionMode})";
        StateChanged?.Invoke();
    }

    public void Deactivate()
    {
        _isZoomActive = false;
        _targetMultiplier = 1.0;

        if (!_isInitialized)
        {
            _currentMultiplier = 1.0;
            StatusMessage = "Idle (1.0x)";
            StateChanged?.Invoke();
            return;
        }

        // Smoothly animate back to 1.0x, or snap immediately if already near 1.0x
        if (Math.Abs(_currentMultiplier - 1.0) < 0.05)
        {
            ApplyIdentityTransform();
        }
        else if (!_updateTimer.IsEnabled)
        {
            _updateTimer.Start();
        }

        StatusMessage = "Returning to 1.0x...";
        StateChanged?.Invoke();
    }

    public void Toggle()
    {
        if (_isZoomActive)
            Deactivate();
        else
            Activate();
    }

    public void SetMultiplier(double multiplier)
    {
        var (valid, error) = _safety.ValidateZoomMultiplier(multiplier);
        if (!valid)
        {
            StatusMessage = error;
            StateChanged?.Invoke();
            return;
        }

        _config.Settings.Zoom.DefaultMultiplier = multiplier;

        if (multiplier <= 1.001)
        {
            Deactivate();
            return;
        }

        if (!_isInitialized && !Initialize())
            return;

        if (!_isZoomActive)
        {
            _isZoomActive = true;
            InitializeCenterPoint();
        }

        _targetMultiplier = multiplier;
        ConfigureTimerInterval();
        if (!_updateTimer.IsEnabled)
            _updateTimer.Start();

        StatusMessage = $"Active ({_targetMultiplier:0.0}x • {PositionMode})";
        StateChanged?.Invoke();
    }

    public void IncreaseZoom()
    {
        var step = Math.Max(0.1, _config.Settings.Zoom.StepSize);
        var baseVal = _isZoomActive ? _targetMultiplier : 1.0;
        var next = Math.Min(_config.Settings.Zoom.MaxMultiplier, Math.Round((baseVal + step) * 10.0) / 10.0);
        SetMultiplier(next);
    }

    public void DecreaseZoom()
    {
        var step = Math.Max(0.1, _config.Settings.Zoom.StepSize);
        var baseVal = _isZoomActive ? _targetMultiplier : 1.0;
        var next = Math.Max(_config.Settings.Zoom.MinMultiplier, Math.Round((baseVal - step) * 10.0) / 10.0);
        if (next <= 1.001)
            Deactivate();
        else
            SetMultiplier(next);
    }

    public void ResetZoom()
    {
        Deactivate();
    }

    public void EmergencyReset()
    {
        if (!_updateTimer.Dispatcher.CheckAccess())
        {
            _updateTimer.Dispatcher.Invoke(EmergencyReset);
            return;
        }

        _updateTimer.Stop();
        _isZoomActive = false;
        _targetMultiplier = 1.0;
        _currentMultiplier = 1.0;
        _lastAppliedMag = 1.0f;
        _lastAppliedX = 0;
        _lastAppliedY = 0;

        if (_isInitialized)
        {
            try { NativeMethods.MagSetFullscreenTransform(1.0f, 0, 0); } catch { }
        }

        StatusMessage = "Reset to 1.0x (Emergency Reset)";
        StateChanged?.Invoke();
    }

    public void ApplyProfileSettings(ZoomSettings settings)
    {
        ConfigureTimerInterval();
        if (_isZoomActive)
        {
            SetMultiplier(Math.Clamp(settings.DefaultMultiplier, settings.MinMultiplier, settings.MaxMultiplier));
        }
        else
        {
            StateChanged?.Invoke();
        }
    }

    private void ConfigureTimerInterval()
    {
        _updateTimer.Interval = _config.Settings.Zoom.ScalingQuality switch
        {
            ScalingQualityMode.LowLatency => TimeSpan.FromMilliseconds(8),
            ScalingQualityMode.Balanced => TimeSpan.FromMilliseconds(16),
            _ => TimeSpan.FromMilliseconds(14)
        };
    }

    private void InitializeCenterPoint()
    {
        var (w, h) = GetPrimaryScreenSize();
        if (PositionMode == ZoomPositionMode.CursorTracking && NativeMethods.GetCursorPos(out var pt))
        {
            _currentCenterX = pt.X;
            _currentCenterY = pt.Y;
            _lastCursorX = pt.X;
            _lastCursorY = pt.Y;
        }
        else if (PositionMode == ZoomPositionMode.ManualCoordinates)
        {
            _currentCenterX = _config.Settings.Zoom.ManualX;
            _currentCenterY = _config.Settings.Zoom.ManualY;
        }
        else
        {
            _currentCenterX = w / 2.0;
            _currentCenterY = h / 2.0;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_isInitialized)
        {
            _updateTimer.Stop();
            return;
        }

        var z = _config.Settings.Zoom;
        var alpha = z.ScalingQuality == ScalingQualityMode.LowLatency
            ? Math.Clamp(z.SmoothingFactor * 1.8, 0.25, 0.95)
            : Math.Clamp(z.SmoothingFactor, 0.08, 0.9);

        // Interpolate zoom multiplier smoothly
        _currentMultiplier += (_targetMultiplier - _currentMultiplier) * alpha;
        if (Math.Abs(_targetMultiplier - _currentMultiplier) < 0.01)
            _currentMultiplier = _targetMultiplier;

        if (!_isZoomActive && _currentMultiplier <= 1.01)
        {
            ApplyIdentityTransform();
            return;
        }

        var (screenW, screenH) = GetPrimaryScreenSize();
        double desiredCenterX;
        double desiredCenterY;

        switch (z.PositionMode)
        {
            case ZoomPositionMode.CursorTracking:
                if (NativeMethods.GetCursorPos(out var cursor))
                {
                    var dx = Math.Abs(cursor.X - _lastCursorX);
                    var dy = Math.Abs(cursor.Y - _lastCursorY);
                    if (dx >= z.CursorDeadzonePx || dy >= z.CursorDeadzonePx)
                    {
                        _lastCursorX = cursor.X;
                        _lastCursorY = cursor.Y;
                    }
                    desiredCenterX = _lastCursorX;
                    desiredCenterY = _lastCursorY;
                }
                else
                {
                    desiredCenterX = _currentCenterX;
                    desiredCenterY = _currentCenterY;
                }
                break;

            case ZoomPositionMode.ManualCoordinates:
                desiredCenterX = z.ManualX;
                desiredCenterY = z.ManualY;
                break;

            default: // ScreenCenter
                desiredCenterX = screenW / 2.0;
                desiredCenterY = screenH / 2.0;
                break;
        }

        _currentCenterX += (desiredCenterX - _currentCenterX) * alpha;
        _currentCenterY += (desiredCenterY - _currentCenterY) * alpha;

        var mag = (float)Math.Clamp(_currentMultiplier, 1.0, z.MaxMultiplier);
        var viewWidth = screenW / mag;
        var viewHeight = screenH / mag;

        var xOffset = _currentCenterX - (viewWidth / 2.0);
        var yOffset = _currentCenterY - (viewHeight / 2.0);

        if (z.ClampToScreenEdges)
        {
            xOffset = Math.Clamp(xOffset, 0, Math.Max(0, screenW - viewWidth));
            yOffset = Math.Clamp(yOffset, 0, Math.Max(0, screenH - viewHeight));
        }

        var rx = (int)Math.Round(xOffset);
        var ry = (int)Math.Round(yOffset);
        if (Math.Abs(mag - _lastAppliedMag) > 0.0005f || rx != _lastAppliedX || ry != _lastAppliedY)
        {
            NativeMethods.MagSetFullscreenTransform(mag, rx, ry);
            _lastAppliedMag = mag;
            _lastAppliedX = rx;
            _lastAppliedY = ry;
        }
    }

    private void ApplyIdentityTransform()
    {
        _updateTimer.Stop();
        _currentMultiplier = 1.0;
        _targetMultiplier = 1.0;
        _isZoomActive = false;
        _lastAppliedMag = 1.0f;
        _lastAppliedX = 0;
        _lastAppliedY = 0;

        if (_isInitialized)
        {
            NativeMethods.MagSetFullscreenTransform(1.0f, 0, 0);
        }

        StatusMessage = "Idle (1.0x)";
        StateChanged?.Invoke();
    }

    private static (int Width, int Height) GetPrimaryScreenSize()
    {
        var w = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        var h = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        return (Math.Max(640, w), Math.Max(480, h));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _updateTimer.Stop();

        if (_isInitialized)
        {
            try { NativeMethods.MagSetFullscreenTransform(1.0f, 0, 0); } catch { }
            try { NativeMethods.MagUninitialize(); } catch { }
            _isInitialized = false;
        }
    }
}
