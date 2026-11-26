using System.IO;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Models;
using Xunit;

namespace ScreenZoomUtility.Tests;

public sealed class SafetyManagerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "SZU_Safety_" + Guid.NewGuid().ToString("N"));
    private readonly ConfigurationManager _config;
    private readonly SafetyManager _safety;

    public SafetyManagerTests()
    {
        _config = new ConfigurationManager(_tempDir);
        _config.LoadAll();
        _safety = new SafetyManager(_config);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
    }

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(2.5, true)]
    [InlineData(8.0, true)]
    [InlineData(0.5, false)]
    [InlineData(9.0, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void ValidateZoomMultiplier_EnforcesBounds(double value, bool expectedValid)
    {
        var (ok, _) = _safety.ValidateZoomMultiplier(value);
        Assert.Equal(expectedValid, ok);
    }

    [Fact]
    public void ValidateSequence_RejectsEmptyAndOverLimitSequences()
    {
        var empty = new KeyboardSequence { Name = "Empty", Steps = [] };
        Assert.False(_safety.ValidateSequence(empty).IsValid);

        var tooLong = new KeyboardSequence
        {
            Name = "Too Long",
            Steps = [new SequenceStep { VirtualKey = 0x41, DelayAfterMs = _config.Settings.Safety.MaxSequenceDurationMs + 1000 }]
        };
        Assert.False(_safety.ValidateSequence(tooLong).IsValid);
    }

    [Fact]
    public void CanStartSequence_BlocksDisabledAndSimultaneousAndCooldown()
    {
        var disabled = new KeyboardSequence
        {
            Name = "Disabled",
            Enabled = false,
            Steps = [new SequenceStep { VirtualKey = 0x41, DelayAfterMs = 100 }]
        };
        Assert.False(_safety.CanStartSequence(disabled).Allowed);

        var valid = new KeyboardSequence
        {
            Name = "Valid",
            Enabled = true,
            Steps = [new SequenceStep { VirtualKey = 0x41, DelayAfterMs = 100 }]
        };
        Assert.True(_safety.CanStartSequence(valid).Allowed);

        _safety.MarkSequenceStarted(valid.Name);
        Assert.False(_safety.CanStartSequence(valid).Allowed);

        _safety.MarkSequenceEnded();
        // Cooldown is 300ms by default, so immediate restart must be blocked
        Assert.False(_safety.CanStartSequence(valid).Allowed);
    }

    [Fact]
    public void TriggerEmergencyStop_NotifiesSubscribers()
    {
        string? received = null;
        _safety.EmergencyStopTriggered += reason => received = reason;

        _safety.TriggerEmergencyStop("Test halt");

        Assert.Equal("Test halt", received);
    }
}
