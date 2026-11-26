using ScreenZoomUtility.Models;

namespace ScreenZoomUtility.Core;

public class SafetyManager
{
    private readonly ConfigurationManager _config;
    private DateTime _lastExecutionCompletedUtc = DateTime.MinValue;
    private volatile bool _isSequenceRunning;

    public event Action<string>? EmergencyStopTriggered;
    public event Action<bool, string>? SequenceActivityChanged;

    public bool IsSequenceRunning => _isSequenceRunning;
    public string ActiveSequenceName { get; private set; } = string.Empty;

    public SafetyManager(ConfigurationManager config)
    {
        _config = config;
    }

    public (bool IsValid, string Error) ValidateZoomMultiplier(double multiplier)
    {
        var z = _config.Settings.Zoom;
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            return (false, "Zoom multiplier must be a finite number.");
        if (multiplier < z.MinMultiplier)
            return (false, $"Zoom multiplier cannot be lower than {z.MinMultiplier:0.0}x.");
        if (multiplier > z.MaxMultiplier)
            return (false, $"Zoom multiplier cannot exceed configured maximum ({z.MaxMultiplier:0.0}x).");
        return (true, string.Empty);
    }

    public (bool IsValid, string Error) ValidateDelay(int delayMs)
    {
        var s = _config.Settings.Safety;
        if (delayMs < s.MinStepDelayMs)
            return (false, $"Delay must be at least {s.MinStepDelayMs} ms.");
        if (delayMs > s.MaxStepDelayMs)
            return (false, $"Delay cannot exceed {s.MaxStepDelayMs} ms.");
        return (true, string.Empty);
    }

    public (bool IsValid, string Error) ValidateSequence(KeyboardSequence sequence)
    {
        var s = _config.Settings.Safety;
        if (sequence.Steps.Count == 0)
            return (false, "Sequence has no steps.");
        if (sequence.Steps.Count > s.MaxStepsPerSequence)
            return (false, $"Sequence exceeds maximum allowed step count ({s.MaxStepsPerSequence}).");

        foreach (var step in sequence.Steps)
        {
            if (step.VirtualKey <= 0 || step.VirtualKey > 0xFE)
                return (false, $"Step '{step.Label}' has an invalid virtual key code.");

            var (ok, err) = ValidateDelay(step.DelayAfterMs);
            if (!ok) return (false, $"Step '{step.Label}': {err}");
        }

        if (sequence.TotalDurationMs > s.MaxSequenceDurationMs)
            return (false, $"Total duration ({sequence.TotalDurationMs} ms) exceeds configured safety limit ({s.MaxSequenceDurationMs} ms).");

        return (true, string.Empty);
    }

    public (bool Allowed, string Reason) CanStartSequence(KeyboardSequence sequence)
    {
        if (!_config.Settings.KeyboardModuleEnabled)
            return (false, "Keyboard module is disabled.");

        var (valid, err) = ValidateSequence(sequence);
        if (!valid)
            return (false, err);

        var s = _config.Settings.Safety;
        if (s.PreventSimultaneousSequences && _isSequenceRunning)
            return (false, $"Another sequence ('{ActiveSequenceName}') is already running.");

        var elapsed = (DateTime.UtcNow - _lastExecutionCompletedUtc).TotalMilliseconds;
        if (elapsed < s.ExecutionCooldownMs)
            return (false, $"Cooldown active ({Math.Ceiling(s.ExecutionCooldownMs - elapsed)} ms remaining).");

        return (true, string.Empty);
    }

    public void MarkSequenceStarted(string sequenceName)
    {
        _isSequenceRunning = true;
        ActiveSequenceName = sequenceName;
        SequenceActivityChanged?.Invoke(true, sequenceName);
    }

    public void MarkSequenceEnded()
    {
        _isSequenceRunning = false;
        ActiveSequenceName = string.Empty;
        _lastExecutionCompletedUtc = DateTime.UtcNow;
        SequenceActivityChanged?.Invoke(false, string.Empty);
    }

    public void TriggerEmergencyStop(string reason = "User triggered emergency stop")
    {
        EmergencyStopTriggered?.Invoke(reason);
    }
}
