using System.Windows.Input;

namespace ScreenZoomUtility.Core;

public static class KeyMapper
{
    public static int ToVirtualKey(Key key)
    {
        if (key == Key.System) return 0;
        return KeyInterop.VirtualKeyFromKey(key);
    }

    public static string ToDisplayName(Key key)
    {
        return key switch
        {
            Key.OemPlus or Key.Add => "Plus",
            Key.OemMinus or Key.Subtract => "Minus",
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            Key.Escape => "Esc",
            Key.Return => "Enter",
            Key.Back => "Backspace",
            _ => key.ToString()
        };
    }

    public static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin or Key.System;
}
