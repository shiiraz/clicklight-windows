# Microsoft Store Readiness

This checklist is for preparing CursorCue for Microsoft Store submission. It keeps publisher-specific values as placeholders so the public repo can be reused by other maintainers.

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
- Store image assets exist in `assets/`, but should be reviewed before each public release.

## Pre-Submission Gates

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\package-msix.ps1 -PrepareOnly
```

Then complete `docs/QA.md` manually.

Do not submit until:

- Manual QA passes on at least one normal Windows 11 setup.
- Mixed-DPI behavior is tested on real hardware.
- Idle CPU returns to approximately 0% after effects finish.
- The settings window does not trigger click overlays over itself.
- The privacy statement is linked from the repository and Store listing.
- The package/install path is signed or Store-managed.

## MSIX Packaging

This repo includes a command-line MSIX scaffold:

- `packaging/msix/AppxManifest.xml.template`
- `package-msix.ps1`

Partner Center assigns Store-specific identity values after reserving a product name. Use those values for Store packages, but do not copy another publisher's values:

```text
Package/Identity/Name: <Partner Center package name>
Package/Identity/Publisher: <Partner Center publisher>
Package/Properties/PublisherDisplayName: <Publisher display name>
Store ID: <Store ID>
Store URL: https://apps.microsoft.com/detail/<Store ID>
Store protocol link: ms-windows-store://pdp/?productid=<Store ID>
```

Prepare the package layout without SDK tools:

```powershell
powershell -ExecutionPolicy Bypass -File .\package-msix.ps1 -PrepareOnly
```

Create an MSIX after installing the Windows SDK/MSIX packaging tools:

```powershell
powershell -ExecutionPolicy Bypass -File .\package-msix.ps1 -Version 0.1.0.0 -SkipSign
```

Store-upload packages can be left unsigned because Microsoft Store signs MSIX packages during submission. Pass the Partner Center identity explicitly when building a Store upload:

```powershell
powershell -ExecutionPolicy Bypass -File .\package-msix.ps1 `
  -Version 0.1.0.0 `
  -PackageName "<Partner Center package name>" `
  -Publisher "<Partner Center publisher>" `
  -PublisherDisplayName "<Publisher display name>" `
  -SkipSign
```

For local signed MSIX testing, override the package identity to match the local development certificate subject:

```powershell
powershell -ExecutionPolicy Bypass -File .\package-msix.ps1 `
  -Version 0.1.0.0 `
  -PackageName "CursorCue.Windows" `
  -Publisher "CN=CursorCue Development" `
  -PublisherDisplayName "CursorCue" `
  -CertificateThumbprint "<local CursorCue development cert thumbprint>" `
  -NoTimestamp
```

Local installation requires signing. Pass either `-CertificateThumbprint` for a certificate in the local cert store, or `-PfxPath` and `-PfxPassword` for a PFX. The signing certificate subject must exactly match the manifest publisher.

For local-only test certificates in offline/restricted-network environments, add `-NoTimestamp`. Do not use `-NoTimestamp` for real release signing.

If `makeappx.exe` is missing, install the Windows SDK or MSIX Packaging Tool first. `-PrepareOnly` still validates the staged layout and manifest.

The packaging script searches the Windows SDK install folders directly, so `Get-Command makeappx.exe` may return nothing even after the SDK is installed. That is fine as long as `package-msix.ps1` can create the package.

Local install smoke test:

```powershell
Add-AppxPackage -Path .\out\msix\CursorCue_0.1.0.0_x64.msix
Get-StartApps | Where-Object { $_.Name -like '*CursorCue*' }
```

Run the install command from a normal user PowerShell or by opening the MSIX in Explorer. Avoid sandboxed shells for this check; they can report `0x80070005` even when the package and signature are valid. If the package was accidentally installed from the wrong/elevated user context, remove that installed package first and reinstall as the user who will run CursorCue.

Launch at Login uses the classic per-user Run key only for unpackaged builds. MSIX builds declare a `windows.startupTask` manifest extension and control it through the Windows StartupTask API, because MSIX virtualizes direct Run-key writes. If Windows reports the task as disabled by the user, CursorCue must send the user to Startup Apps instead of forcing it back on. Verify this behavior during final Store QA with a full restart, not only sign-out/sign-in.

## Store Listing Notes

Keep public listing copy accurate, current, and limited to shipped behavior. Store screenshots should show the current app UI and should not expose private desktop content. Keep Partner Center values, certificate details, account emails, and local machine paths out of repository files.
