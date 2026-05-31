# Contributing to CursorCue

Thanks for taking a look. CursorCue is a small Windows tray utility, so changes should stay focused, native, and light while idle.

## Local setup

Requirements:

- Windows 11.
- Windows PowerShell.
- .NET Framework available on the machine.

Build and run without signing:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\bin\CursorCue.exe
```

No SDK, Store account, certificate, or MSIX signing is required for normal local development. Use **Quit CursorCue** from the tray menu before rebuilding so `bin\CursorCue.exe` is not locked.

Run automated checks:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
```

## Where to work

- `src/App` app lifetime and single-instance behavior.
- `src/Core` click event types, clock abstractions, and shared timing concepts.
- `src/Input` global mouse hook capture.
- `src/Overlay` click-through overlay windows, click pulses, drag effects, and laser pointer drawing.
- `src/Settings` settings schema, defaults, presets, color presets, persistence, and migrations.
- `src/UI` tray menu, tray icon, and settings window.
- `src/System` launch-at-login behavior.
- `src/Interop` Win32 declarations.
- `tests` automated spec checks.

## Behavior rules

- Keep idle overhead low. Do not add polling loops or permanent animation loops.
- Overlay drawing should happen only while click effects or laser strokes are active.
- Keep settings keys stable unless a migration is included.
- Treat the README credit to ClickLight for macOS as intentional. CursorCue is independent, but its visual behavior began from that inspiration.
- Keep Microsoft Store and publisher-specific values out of public code and docs. Use placeholders in examples.

## Pull requests

For bug fixes, include:

- What broke.
- How to reproduce it.
- What changed.
- Any manual checks you ran.

For features, include:

- Why the feature belongs in a small click-highlighting utility.
- Whether it affects idle CPU, input capture, overlay rendering, settings, or packaging.
- Screenshots or a short recording for visible UI/overlay changes.

Before opening a PR, please run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

If you changed overlay behavior, also manually test press, release, right-click, drag, laser pointer mode, and **Test Pulse at Pointer** in at least one normal desktop app.

## Packaging notes

Packaging is optional for contributors. `package-msix.ps1` exists for Store/MSIX workflows, but normal code changes can be built and tested from `bin\CursorCue.exe`.

If you do work on packaging, keep Partner Center package names, publisher IDs, Store IDs, certificate thumbprints, PFX files, and local machine paths out of commits. See `docs/STORE_READINESS.md` for the generic flow.
