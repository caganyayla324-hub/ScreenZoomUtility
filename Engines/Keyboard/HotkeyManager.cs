using System.Windows.Interop;
using ScreenZoomUtility.Core;
using ScreenZoomUtility.Models;

namespace ScreenZoomUtility.Engines.Keyboard;

public sealed class HotkeyManager : IDisposable
{
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private readonly Dictionary<int, HotkeyBinding> _registeredByAtom = [];
    private int _nextId = 1000;
    private bool _disposed;

    public event Action<HotkeyBinding>? HotkeyPressed;
    public IReadOnlyDictionary<string, string> RegistrationErrors => _registrationErrors;
    private readonly Dictionary<string, string> _registrationErrors = [];

    public void AttachToWindow(IntPtr hwnd)
    {
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
        }

        _windowHandle = hwnd;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
    }

    public List<string> DetectConflicts(IEnumerable<HotkeyBinding> bindings)
    {
        var conflicts = new List<string>();
        var groups = bindings
            .Where(b => b.Enabled && b.VirtualKey > 0)
            .GroupBy(b => b.Signature)
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            var names = string.Join(", ", group.Select(b => b.ActionName));
            var shortcut = group.First().FormattedShortcut;
            conflicts.Add($"Conflict on [{shortcut}]: assigned to {names}");
        }

        return conflicts;
    }

    public void ApplyBindings(IEnumerable<HotkeyBinding> bindings)
    {
        UnregisterAll();
        _registrationErrors.Clear();

        if (_windowHandle == IntPtr.Zero) return;

        var seenSignatures = new HashSet<string>();

        foreach (var binding in bindings)
        {
            if (!binding.Enabled || binding.VirtualKey <= 0)
                continue;

            if (!seenSignatures.Add(binding.Signature))
            {
                _registrationErrors[binding.Id] = $"Duplicate shortcut [{binding.FormattedShortcut}] skipped for '{binding.ActionName}'.";
                continue;
            }

            uint mods = NativeMethods.MOD_NOREPEAT;
            if (binding.Ctrl) mods |= NativeMethods.MOD_CONTROL;
            if (binding.Alt) mods |= NativeMethods.MOD_ALT;
            if (binding.Shift) mods |= NativeMethods.MOD_SHIFT;
            if (binding.Win) mods |= NativeMethods.MOD_WIN;

            var id = _nextId++;
            var ok = NativeMethods.RegisterHotKey(_windowHandle, id, mods, (uint)binding.VirtualKey);
            if (ok)
            {
                _registeredByAtom[id] = binding;
            }
            else
            {
                _registrationErrors[binding.Id] = $"Could not register [{binding.FormattedShortcut}] (may be in use by Windows or another app).";
            }
        }
    }

    public void UnregisterAll()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            foreach (var id in _registeredByAtom.Keys)
            {
                NativeMethods.UnregisterHotKey(_windowHandle, id);
            }
        }

        _registeredByAtom.Clear();
        _nextId = 1000;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_registeredByAtom.TryGetValue(id, out var binding))
            {
                HotkeyPressed?.Invoke(binding);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }
}
