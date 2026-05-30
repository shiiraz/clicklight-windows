# Microsoft Store Readiness

This checklist is for preparing ClickLight for Microsoft Store submission. It intentionally excludes final image assets for now.

## Current Recommendation

Use **MSIX** for the first Store submission if possible.

Microsoft documents MSIX as the recommended Microsoft Store path for new Windows apps. MSI/EXE submission is also supported, but it requires a publisher-signed installer with a certificate chaining to a CA in the Microsoft Trusted Root Program, and Store-managed updates are not available for that path.

References:

- Microsoft Store distribution paths: https://learn.microsoft.com/windows/apps/package-and-deploy/choose-distribution-path
- MSIX Packaging Tool: https://learn.microsoft.com/windows/msix/packaging-tool/tool-overview
- MSI/EXE Store submission: https://learn.microsoft.com/windows/apps/publish/publish-your-app/msi/create-app-submission
- Store screenshots and images: https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/screenshots-and-images

## App Status Before Packaging

- Core click overlay behavior is implemented.
- Tray menu is implemented.
- Settings window is implemented.
- Per-monitor overlay windows are implemented.
- Automated spec tests exist in `tests/`.
- Manual QA checklist exists in `docs/QA.md`.
- Privacy statement exists in `PRIVACY.md`.
- Final image assets are intentionally not included yet.

## Pre-Submission Gates

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Then complete `docs/QA.md` manually.

Do not submit until:

- Manual QA passes on at least one normal Windows 11 setup.
- Mixed-DPI behavior is tested on real hardware.
- Idle CPU returns to approximately 0% after effects finish.
- The settings window does not trigger click overlays over itself.
- The privacy statement is linked from the repository and Store listing.
- The package/install path is signed or Store-managed.

## Store Listing Draft

Short description:

```text
Native Windows tray app for live click highlights.
```

Description:

```text
ClickLight is a small Windows tray utility that makes mouse clicks easier to follow during live demos, meetings, UX reviews, tutorials, and recordings.

It highlights press, release, right-click, and drag interactions, includes an optional laser pointer mode, and stays light when idle by drawing only while visual effects are active.

ClickLight runs locally, stores settings on your device, and does not collect or transmit personal data.
```

Feature bullets:

```text
Live click highlights for demos and screen sharing
Distinct visuals for press, release, right-click, and drag
Optional laser pointer mode with fading strokes
Tray menu for quick toggles and presets
Native settings window with preview and reset
No always-on render loop while idle
Local settings only; no network requests
```

Keywords:

```text
click highlighter
cursor
mouse
presentation
demo
screen sharing
tutorial
```

Additional system requirement:

```text
Some elevated apps may not expose clicks to non-elevated ClickLight because of Windows UIPI protections.
```

## Asset Requirements To Hand Off

Do not generate final assets in this repository yet. The asset agent should produce:

- App icon/source logo.
- Store 1:1 tile icon, 300 x 300 PNG.
- At least 4 desktop screenshots, 1366 x 768 or larger PNG.
- Optional 16:9 hero art, 1920 x 1080 PNG.
- Optional short demo trailer, MP4, 1920 x 1080.

## Asset Prompts

Use these with an image/design agent later.

### App Icon

```text
Create a polished Windows 11 app icon for "ClickLight", a native tray utility that highlights mouse clicks during demos. Visual concept: clean cursor pointer with a subtle cyan click ripple/ring, minimal, modern, friendly, high contrast, works at 16px tray size and 256px app size. Avoid text, gradients that become muddy, mascot characters, and overly detailed UI screenshots. Provide transparent PNG and scalable source.
```

### Store Tile

```text
Create a Microsoft Store 1:1 tile image for ClickLight at 300x300. Show a crisp cursor pointer and click ripple on a calm Windows 11-style background. The image should feel lightweight, precise, and demo-focused. No marketing copy. Keep the main symbol centered and readable at small sizes.
```

### Screenshots

```text
Create a clean 1366x768 Windows 11 desktop screenshot mockup showing ClickLight in use during a product demo. The cursor is clicking a small UI button and a tasteful cyan ripple is visible around the pointer. Include the tray menu or settings window in some screenshots, but keep the app focused and uncluttered. Avoid fake claims, heavy marketing text, and unrelated UI.
```

### Hero Art

```text
Create 1920x1080 Microsoft Store hero art for ClickLight. Show a Windows 11 desktop demo scene where a cursor click is clearly highlighted by an elegant ripple. The composition should communicate "easy to follow live demos" without text. Keep important visual elements in the top two-thirds and leave the lower third clean for Store overlays.
```

