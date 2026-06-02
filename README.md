<p align="center">
  <img src="assets/logo/cursorcue-logo-clean-128.png" width="96" height="96" alt="CursorCue icon">
</p>

<h1 align="center">CursorCue</h1>

Native Windows 11 tray utility that highlights mouse clicks during live demos, screen sharing, UX reviews, and recordings.

CursorCue is intentionally small: it lives in the notification area, draws click highlights only when needed, and avoids a permanent render loop while idle.

CursorCue is an early Windows app built from the behavior and visual timing of ClickLight for macOS, then cleaned up into a Windows-first repository. It is an independent rewrite, not an official ClickLight port. Maintenance is best-effort.

<p>
  <a href="https://get.microsoft.com/installer/download/9pd8w85g4ms1?referrer=appbadge" target="_self">
    <img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200" alt="Get it from Microsoft">
  </a>
</p>

## Demo

https://github.com/user-attachments/assets/4c8edddc-1bc5-496b-8b85-a3704f8484cc


## Origin and Credits

CursorCue began from a fork of Aurora Scharff's MIT-licensed ClickLight for macOS: https://github.com/aurorascharff/ClickLight

The Windows app is maintained independently and may diverge, but the original project remains the behavioral and visual inspiration.

The original MIT license notice is retained in `LICENSE`.

Privacy notes are in `PRIVACY.md`.

Contributing notes are in `CONTRIBUTING.md`.

## Status

Implemented:

- Notification-area tray icon with the fixed menu order.
- Enabled toggle, capture-status tooltip toggle, launch-at-login toggle, preset/color menu persistence, test pulse, and clean quit.
- Global low-level mouse capture using `WH_MOUSE_LL`.
- Press, release, right-click, drag, and laser pointer visuals using the original easing, default colors, size, intensity, and duration math ported to GDI+.
- Event dedupe: same kind, within 3 px, inside 0.1 s.
- Rendering timer runs at 60 fps only while an overlay pulse or laser visual is active.
- Settings values are clamped to the documented ranges when loaded or saved.
- Settings window with General, Visual Style, Event Visibility, Tray, and System panes.
- Settings Preview Pulse and Reset to Defaults.
- Per-monitor overlay windows with display-change rebuilds.
- First launch opens Settings so Store/Start users can see where the app lives; later launches stay tray-only.
- Single-instance launch behavior: reopening CursorCue focuses Settings instead of creating a second tray process.

Not finished yet:

- Richer overlay parity validation.
- Mixed-DPI validation on real hardware.
- Installer, signing, and update flow.

## Build

The current build uses the .NET Framework compiler bridge built into Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The executable is written to:

```text
bin\CursorCue.exe
```

Run it from PowerShell or Explorer. It lives in the notification area and does not create a taskbar button. On a fresh install, the Settings window opens once; after that CursorCue starts quietly in the tray.

No signing is required for local source builds. Signing is only needed if you want to install an MSIX package locally instead of running `bin\CursorCue.exe` directly.

## Local Development

Prerequisites:

- Windows 11.
- Windows PowerShell.
- .NET Framework available on the machine. No separate SDK is required for the current build script.

Build and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\bin\CursorCue.exe
```

CursorCue runs from the notification area. Use **Quit CursorCue** from the tray menu before rebuilding; otherwise `bin\CursorCue.exe` may be locked by the running process.

If CursorCue is already running, launching `CursorCue.exe` again focuses the existing Settings window instead of starting another copy.

For a clean rebuild, delete `bin\` and run the build script again.

Source layout:

- `src/App` app wiring.
- `src/Core` event types and timing.
- `src/Input` global mouse capture.
- `src/Overlay` click-through overlay windows and drawing.
- `src/Settings` settings model, presets, colors, and persistence.
- `src/UI` tray icon/menu and settings window.
- `src/Interop` Win32 declarations.
- `src/System` launch-at-login integration.

Manual checks before release:

- Launch from `bin\CursorCue.exe`, open the tray menu, and quit cleanly.
- Open Settings and check each pane.
- Test press, release, right-click, drag, and laser pointer mode in common apps.
- Use **Test Pulse at Pointer** from the tray.
- Use **Preview Pulse** and **Reset to Defaults** from Settings.
- Confirm settings persist after quitting and relaunching.
- Try at least one multi-monitor or mixed-DPI setup before publishing a release build.

For the fuller manual checklist, see `docs/QA.md`.

For contributing guidelines, see `CONTRIBUTING.md`.

Automated checks:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
```

Packaging is optional for contributors. Normal development does not require MSIX packaging or signing. For maintainer packaging notes, see `docs/STORE_READINESS.md`.

## Scope

CursorCue is not trying to become a presentation suite. The target is a focused, native click highlighter with polished timing, low idle overhead, and minimal UI.

## Settings

Settings are persisted as JSON at:

```text
%AppData%\CursorCue\settings.json
```

The JSON keys are intentionally stable and still match the ClickLight-derived settings schema: `isEnabled`, `showPress`, `showRelease`, `showRightClick`, `showDrag`, `showLaserPointer`, `showMenuBarText`, `size`, `intensity`, `duration`, `colorPreset`, `customColorRed`, `customColorGreen`, and `customColorBlue`.

## Known Windows limitations

- Clicks in elevated processes may not be visible to a non-elevated CursorCue process because of UIPI.
- Exclusive fullscreen apps may cover or bypass normal topmost overlays.
- Mixed-DPI and unusual multi-monitor arrangements need explicit testing and hardening.
- Smart App Control may block local builds because `bin\CursorCue.exe` is currently unsigned and has no reputation. There is no per-app bypass for Smart App Control; proper release builds should be signed with a trusted code-signing certificate.

## Maintenance

This Windows port is maintained best-effort. Some implementation work is agent-assisted, with changes reviewed before release.

## Architecture decisions

| Layer | Choice | Why |
| --- | --- | --- |
| Language/runtime | C# on built-in .NET Framework via PowerShell `Add-Type` | This keeps the app native and buildable on a plain Windows setup without installing a separate SDK. |
| UI loop | WinForms `ApplicationContext` | It gives a native Windows message loop, tray support, timers, and Win32 interop with very little scaffolding. |
| Tray | `System.Windows.Forms.NotifyIcon` | It maps directly to the Windows notification area and avoids extra dependencies. |
| Single instance | Named mutex + named activation event | A second launch exits immediately after signaling the running tray process to open Settings. This avoids duplicate tray icons without adding IPC dependencies or idle polling. |
| Global input | `WH_MOUSE_LL` | Matches the requested system-wide, non-blocking capture model. The hook callback converts events and returns immediately. |
| Overlay rendering | Per-monitor click-through layered HWNDs + GDI+ into `UpdateLayeredWindow` | Direct2D would be a stronger long-term renderer, but GDI+ is dependency-free here and can faithfully port the pulse geometry/easing. The overlay timer is stopped whenever there is nothing to draw. |
| Persistence | JSON in `%AppData%\CursorCue` with one-time migration from `%AppData%\ClickLight` if present | Human-readable, easy to inspect, and the schema preserves the original setting keys while avoiding old product branding for new installs. |
| Settings UI | WinForms native window | WPF has stronger styling options, but WinForms keeps one build path and supports the 760x520 layout target without adding another runtime or SDK requirement. |
| Launch at login | Classic Run key for unpackaged builds; MSIX `startupTask` extension for packaged builds | MSIX virtualizes direct Run-key writes, so Store builds must use the Windows-supported startup task path. The unpackaged dev build keeps the simple per-user Run key. |
| Store packaging | Manual MSIX layout + MakeAppx script | This keeps packaging explicit for a small WinForms app and avoids taking a Visual Studio packaging-project dependency. Store builds must replace the local placeholder package identity with Partner Center values. |
| Updates | Not Configured stub | Installer/update strategy should wait for signing/packaging decisions. |
