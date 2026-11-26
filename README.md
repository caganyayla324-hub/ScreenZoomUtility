# Advanced Windows Screen Zoom & Keyboard Utility

A standalone Windows desktop accessibility and productivity utility built with **C# / .NET 10 (WPF)** using strictly documented Windows APIs.

## Modules

### 1. Full-Screen Zoom Engine
- Uses the documented **Windows Magnification API** (`Magnification.dll`: `MagInitialize`, `MagSetFullscreenTransform`).
- Operates strictly at the Windows desktop compositor level — completely independent of all running applications and games.
- **Zoom Levels**: Preset buttons (`1.0x`, `1.5x`, `2.0x`, `2.5x`, `3.0x`, `4.0x`, `5.0x`, `6.0x`, `8.0x`) plus custom multipliers up to the configured hardware limit.
- **Positioning Modes**:
  1. Smooth mouse cursor tracking (with deadzone & screen-edge clamping)
  2. Screen center positioning
  3. Manual screen coordinates `(X, Y)`
- **Zero idle overhead**: Timer runs only while magnification is active; automatically resets to `1.0x` on deactivation, emergency stop, or exit.

### 2. General-Purpose Keyboard Utility
- Uses documented **User32** APIs (`RegisterHotKey` / `WM_HOTKEY` and `SendInput`).
- **Visual Sequence Editor**: Add, edit, reorder, remove, save, duplicate, and run productivity key sequences with independent per-step millisecond delays and preset delay buttons (`50ms`–`1000ms`).
- **Global Hotkey Manager**: Custom modifier combinations (`Ctrl`, `Alt`, `Shift`, `Win`), conflict detection, enable/disable toggles, and profile-specific bindings.
- **Safety Controls**:
  - Immediate **Emergency Stop All** shortcut (`Ctrl + Alt + Esc` by default) and sidebar button.
  - Configurable maximum sequence duration, min/max step delays, execution cooldown, and re-entrancy protection.
  - Visible header banner whenever a sequence is executing.

## Default Global Shortcuts

| Action | Default Shortcut |
| :--- | :--- |
| Toggle Screen Zoom | `Ctrl + Alt + Z` |
| Deactivate Screen Zoom | `Ctrl + Alt + X` |
| Increase Magnification | `Ctrl + Alt + Plus` |
| Decrease Magnification | `Ctrl + Alt + Minus` |
| Reset Magnification (`1.0x`) | `Ctrl + Alt + 0` |
| Switch Zoom Profile | `Ctrl + Alt + P` |
| **Emergency Stop & Reset All** | **`Ctrl + Alt + Esc`** |

## Building & Running

```powershell
# Build Release
dotnet build ScreenZoomUtility\ScreenZoomUtility.csproj -c Release

# Run
dotnet run --project ScreenZoomUtility\ScreenZoomUtility.csproj -c Release
```

Configuration files (`Settings.json` and `Profiles.json`) are stored in `%LOCALAPPDATA%\ScreenZoomUtility\`.

## Security & Independence
- No process memory reading or modification.
- No DLL injection, rendering hooks, or kernel drivers.
- No game-specific logic, state inspection, or anti-cheat interaction.
- Transparent, user-controlled desktop utility only.
