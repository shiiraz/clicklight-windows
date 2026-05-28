# ClickLight

Native Windows 11 tray utility that highlights mouse clicks during live demos, screen sharing, UX reviews, and recordings.

ClickLight is intentionally small: it lives in the notification area, draws click highlights only when needed, and avoids a permanent render loop while idle.

This is an early Windows port. It was built from the behavior and visual timing of the original ClickLight app, then cleaned up into a Windows-first repository. Maintenance is best-effort.

## Origin and Credits

This Windows port is derived from the original macOS ClickLight project by Aurora Scharff: https://github.com/aurorascharff/ClickLight

The original MIT license notice is retained in `LICENSE`.

## Status

Implemented:

- Notification-area tray icon with the fixed menu order.
- Enabled toggle, tray label tooltip toggle, launch-at-login toggle, preset/color menu persistence, test pulse, and clean quit.
- Global low-level mouse capture using `WH_MOUSE_LL`.
- Press, release, right-click, drag, and laser pointer visuals using the original easing, default colors, size, intensity, and duration math ported to GDI+.
- Event dedupe: same kind, within 3 px, inside 0.1 s.
- Rendering timer runs at 60 fps only while an overlay pulse or laser visual is active.
- Settings values are clamped to the documented ranges when loaded or saved.
- Settings window with General, Visual Style, Event Visibility, Tray, and System panes.
- Settings Preview Pulse and Reset to Defaults.

Not finished yet:

- Richer overlay parity validation.
- Multi-monitor and mixed-DPI hardening.
- Installer, signing, and update flow.

## Build

The current build uses the .NET Framework compiler bridge built into Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The executable is written to:

```text
bin\ClickLight.exe
```

Run it from PowerShell or Explorer. It lives in the notification area and does not create a taskbar button.

## Local Development

Prerequisites:

- Windows 11.
- Windows PowerShell.
- .NET Framework available on the machine. No separate SDK is required for the current build script.

Build and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\bin\ClickLight.exe
```

ClickLight runs from the notification area. Use **Quit ClickLight** from the tray menu before rebuilding; otherwise `bin\ClickLight.exe` may be locked by the running process.

For a clean rebuild, delete `bin\` and run the build script again.

Manual checks before release:

- Launch from `bin\ClickLight.exe`, open the tray menu, and quit cleanly.
- Open Settings and check each pane.
- Test press, release, right-click, drag, and laser pointer mode in common apps.
- Use **Test Pulse at Pointer** from the tray.
- Use **Preview Pulse** and **Reset to Defaults** from Settings.
- Confirm settings persist after quitting and relaunching.
- Try at least one multi-monitor or mixed-DPI setup before publishing a release build.

## Scope

ClickLight is not trying to become a presentation suite. The target is a focused, native click highlighter with polished timing, low idle overhead, and minimal UI.

## Settings

Settings are persisted as JSON at:

```text
%AppData%\ClickLight\settings.json
```

The JSON keys are intentionally stable: `isEnabled`, `showPress`, `showRelease`, `showRightClick`, `showDrag`, `showLaserPointer`, `showMenuBarText`, `size`, `intensity`, `duration`, `colorPreset`, `customColorRed`, `customColorGreen`, and `customColorBlue`.

## Known Windows limitations

- Clicks in elevated processes may not be visible to a non-elevated ClickLight process because of UIPI.
- Exclusive fullscreen apps may cover or bypass normal topmost overlays.
- Mixed-DPI and unusual multi-monitor arrangements need explicit testing and hardening.
- Smart App Control may block local builds because `bin\ClickLight.exe` is currently unsigned and has no reputation. There is no per-app bypass for Smart App Control; proper release builds should be signed with a trusted code-signing certificate.

## Maintenance

This Windows port is maintained best-effort. Some implementation work is agent-assisted, with changes reviewed before release.

## Architecture decisions

| Layer | Choice | Why |
| --- | --- | --- |
| Language/runtime | C# on built-in .NET Framework via PowerShell `Add-Type` | This keeps the app native and buildable on a plain Windows setup without installing a separate SDK. |
| UI loop | WinForms `ApplicationContext` | It gives a native Windows message loop, tray support, timers, and Win32 interop with very little scaffolding. |
| Tray | `System.Windows.Forms.NotifyIcon` | It maps directly to the Windows notification area and avoids extra dependencies. |
| Global input | `WH_MOUSE_LL` | Matches the requested system-wide, non-blocking capture model. The hook callback converts events and returns immediately. |
| Overlay rendering | Click-through layered HWND + GDI+ into `UpdateLayeredWindow` | Direct2D would be a stronger long-term renderer, but GDI+ is dependency-free here and can faithfully port the pulse geometry/easing for Phase 1. The overlay timer is stopped whenever there is nothing to draw. |
| Persistence | JSON in `%AppData%\ClickLight` | Human-readable, easy to inspect, and the schema preserves the original setting keys. |
| Settings UI | WinForms native window | WPF has stronger styling options, but WinForms keeps one build path and supports the 760x520 layout target without adding another runtime or SDK requirement. |
| Launch at login | HKCU `Software\Microsoft\Windows\CurrentVersion\Run` | This is the simplest per-user Windows startup mechanism for an unpackaged tray utility. |
| Updates | Not Configured stub | Installer/update strategy should wait for signing/packaging decisions. |
