# ClickLight QA Checklist

Use this before publishing builds or claiming parity with the original behavior. Some tests need real human observation because Windows hooks, overlays, Smart App Control, and display scaling cannot be trusted from unit tests alone.

## Automated Checks

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
```

Current automated coverage:

- Settings defaults.
- Settings range clamping.
- Color preset metadata and default kind colors.
- Numeric presets.
- Pulse progress and expiry.
- Laser cursor fade timing.
- Laser stroke append threshold and fade timing.

## Manual Smoke Test

- Build with `powershell -ExecutionPolicy Bypass -File .\build.ps1`.
- Launch `bin\ClickLight.exe`.
- Confirm it appears only in the notification area, not the taskbar.
- On a fresh settings profile, confirm Settings opens automatically once so the user can find the tray app.
- Open the tray menu.
- Click **Test Pulse at Pointer**.
- Launch `bin\ClickLight.exe` again while ClickLight is already running.
- Confirm a second process/tray icon is not created and the existing Settings window opens or focuses.
- Toggle **Enabled** off and confirm clicks no longer show overlays.
- Toggle **Enabled** on and confirm overlays return.
- Quit from **Quit ClickLight** and confirm the process exits.

## Common App Capture

Test in each app:

- Notepad.
- File Explorer.
- A Chromium-based browser.
- A non-elevated app with standard controls.

For each app:

- Left mouse down shows press pulse.
- Left mouse up shows release pulse.
- Right-click shows right-click visual.
- Drag shows drag trail when laser mode is off.
- Rapid repeated clicks do not duplicate within the 3 px / 0.1 s dedupe window.

## Laser Mode

- Turn on **Laser Pointer Mode** from tray.
- Move the mouse without pressing; confirm red laser cursor follows/fades.
- Drag with left button; confirm a red stroke is drawn.
- Release; confirm the stroke fades out.
- Confirm normal drag trail is disabled while laser mode is on.
- Turn laser mode off; confirm normal drag trail returns if **Show Drag** is enabled.

## Settings Window

- Open **Open Settings...**.
- Confirm the window is approximately 760x520 and resizable down to a usable minimum.
- Visit General, Visual Style, Event Visibility, Tray, and System panes.
- Toggle **Enable ClickLight** and confirm tray state follows.
- Use **Preview Pulse** and confirm pulse appears at the pointer.
- Change size/intensity/duration sliders and confirm the tray menu shows custom state where appropriate.
- Choose each size/intensity/duration preset and confirm the corresponding slider value changes.
- Choose each color preset and confirm pulse color changes.
- Pick a custom color and confirm preset switches to Custom.
- Toggle press/release/right-click/drag and confirm behavior matches.
- Enable laser mode and confirm the drag toggle is disabled.
- Use **Reset to Defaults** and confirm defaults are restored.
- Click inside the settings window and confirm no overlay pulse appears over the settings window.

## Persistence

- Change multiple settings.
- Quit ClickLight.
- Relaunch ClickLight.
- Confirm settings persisted.
- Inspect `%AppData%\ClickLight\settings.json` and confirm keys are present.

## Multi-Monitor and DPI

Run these on real hardware if possible:

- Single monitor at 100%.
- Single monitor at 125% or 150%.
- Two monitors with the same scale.
- Two monitors with mixed scale, for example 100% + 150%.
- Secondary monitor positioned left of the primary.
- Secondary monitor positioned above the primary.

For each setup:

- Clicks render on the correct monitor.
- Pulses are centered at the pointer.
- Settings window clicks are ignored.
- Display connect/disconnect or scale changes do not leave stale overlays.

## Idle Resource Check

- Launch ClickLight.
- Do not click or move the mouse for 60 seconds.
- Confirm CPU is approximately 0%.
- Trigger a few pulses, then wait for animations to finish.
- Confirm CPU returns to approximately 0%.

## Windows Security / Elevation

- Try clicking inside a normal non-elevated app.
- Try clicking inside an elevated app, such as an administrator Command Prompt.
- Confirm any missing elevated-app capture is documented as a UIPI limitation.
- On a system with Smart App Control enabled, confirm unsigned local builds may be blocked.

## Edge Cases

- Very fast double-clicks.
- Clicks near screen edges and corners.
- Clicks while the tray menu is open.
- Clicks immediately after toggling enabled/laser mode.
- Press on one monitor and drag/release on another.
- Long drag strokes.
- Middle/extra mouse button drag movement should show drag visuals, while button down/up pulses are only specified for left/right.
- Sleep/resume.
- Display plug/unplug while ClickLight is running.
- Restart Explorer while ClickLight is running.
