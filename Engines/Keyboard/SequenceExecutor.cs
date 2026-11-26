using System.Diagnostics;
using System.Runtime.InteropServices;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Models;

namespace ScreenZoomUtility.Engines.Keyboard;

public sealed class SequenceExecutor : IDisposable
{
    private readonly SafetyManager _safety;
    private readonly object _lock = new();
    private CancellationTokenSource? _activeCts;
    private bool _disposed;

    public event Action<string>? ExecutionLog;
    public event Action<int, int, string>? StepProgress;

    public SequenceExecutor(SafetyManager safety)
    {
        _safety = safety;
        _safety.EmergencyStopTriggered += reason =>
        {
            CancelCurrent();
            ExecutionLog?.Invoke($"[EMERGENCY STOP] {reason}");
        };
    }

    public async Task<(bool Started, string Message)> ExecuteAsync(KeyboardSequence sequence)
    {
        var (allowed, reason) = _safety.CanStartSequence(sequence);
        if (!allowed)
        {
            ExecutionLog?.Invoke($"[Blocked] {sequence.Name}: {reason}");
            return (false, reason);
        }

        CancellationTokenSource cts;
        lock (_lock)
        {
            _activeCts?.Cancel();
            _activeCts?.Dispose();
            _activeCts = new CancellationTokenSource();
            cts = _activeCts;
        }

        _safety.MarkSequenceStarted(sequence.Name);
        ExecutionLog?.Invoke($"[Start] Running '{sequence.Name}' ({sequence.Steps.Count} step(s))");

        var sw = Stopwatch.StartNew();

        try
        {
            for (var i = 0; i < sequence.Steps.Count; i++)
            {
                cts.Token.ThrowIfCancellationRequested();

                var step = sequence.Steps[i];
                StepProgress?.Invoke(i + 1, sequence.Steps.Count, $"{step.Label} ({step.ShortcutSummary})");
                var (sent, expected) = SendKeyStep(step);
                if (sent < expected)
                {
                    ExecutionLog?.Invoke($"  [Warning] Step {i + 1}: Only {sent}/{expected} input event(s) delivered (target window may be elevated/UIPI-protected).");
                }
                else
                {
                    ExecutionLog?.Invoke($"  Step {i + 1}/{sequence.Steps.Count}: {step.ShortcutSummary} (wait {step.DelayAfterMs} ms)");
                }

                if (step.DelayAfterMs > 0)
                {
                    await Task.Delay(step.DelayAfterMs, cts.Token);
                }
            }

            ExecutionLog?.Invoke($"[Done] Completed '{sequence.Name}' in {sw.ElapsedMilliseconds} ms.");
            return (true, "Completed");
        }
        catch (OperationCanceledException)
        {
            ExecutionLog?.Invoke($"[Cancelled] '{sequence.Name}' halted at {sw.ElapsedMilliseconds} ms.");
            return (false, "Cancelled");
        }
        catch (Exception ex)
        {
            ExecutionLog?.Invoke($"[Error] '{sequence.Name}' failed: {ex.Message}");
            return (false, ex.Message);
        }
        finally
        {
            lock (_lock)
            {
                if (ReferenceEquals(_activeCts, cts))
                {
                    _activeCts.Dispose();
                    _activeCts = null;
                }
            }
            StepProgress?.Invoke(0, 0, string.Empty);
            _safety.MarkSequenceEnded();
        }
    }

    public void CancelCurrent()
    {
        lock (_lock)
        {
            try { _activeCts?.Cancel(); } catch { }
        }
    }

    private static (uint Sent, uint Expected) SendKeyStep(SequenceStep step)
    {
        var inputs = new List<NativeMethods.INPUT>();

        if (step.Ctrl) inputs.Add(CreateKeyInput(0x11, false));
        if (step.Alt) inputs.Add(CreateKeyInput(0x12, false));
        if (step.Shift) inputs.Add(CreateKeyInput(0x10, false));
        if (step.Win) inputs.Add(CreateKeyInput(0x5B, false));

        inputs.Add(CreateKeyInput((ushort)step.VirtualKey, false));
        inputs.Add(CreateKeyInput((ushort)step.VirtualKey, true));

        if (step.Win) inputs.Add(CreateKeyInput(0x5B, true));
        if (step.Shift) inputs.Add(CreateKeyInput(0x10, true));
        if (step.Alt) inputs.Add(CreateKeyInput(0x12, true));
        if (step.Ctrl) inputs.Add(CreateKeyInput(0x11, true));

        var arr = inputs.ToArray();
        var sent = NativeMethods.SendInput((uint)arr.Length, arr, Marshal.SizeOf<NativeMethods.INPUT>());
        return (sent, (uint)arr.Length);
    }

    private static NativeMethods.INPUT CreateKeyInput(ushort vk, bool keyUp)
    {
        var scan = (ushort)NativeMethods.MapVirtualKey(vk, 0);
        uint flags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0;

        // Extended keys (arrows, navigation cluster, RWin/LWin)
        if (vk is >= 0x21 and <= 0x2E or 0x5B or 0x5C)
        {
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        }

        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelCurrent();
        lock (_lock)
        {
            _activeCts?.Dispose();
            _activeCts = null;
        }
    }
}
