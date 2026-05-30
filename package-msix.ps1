param(
    [string]$Version = "0.1.0.0",
    [string]$PackageName = "ClickLight.Windows",
    [string]$Publisher = "CN=ClickLight Development",
    [string]$PublisherDisplayName = "ClickLight",
    [string]$DisplayName = "ClickLight",
    [string]$Description = "Native Windows tray app for live click highlights.",
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Architecture = "x64",
    [string]$CertificateThumbprint = "",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [switch]$NoTimestamp,
    [switch]$SkipSign,
    [switch]$PrepareOnly
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutRoot = Join-Path $Root "out\msix"
$LayoutDir = Join-Path $OutRoot "layout"
$ManifestTemplate = Join-Path $Root "packaging\msix\AppxManifest.xml.template"
$ManifestOut = Join-Path $LayoutDir "AppxManifest.xml"
$PackagePath = Join-Path $OutRoot ("ClickLight_{0}_{1}.msix" -f $Version, $Architecture)

function Find-SdkTool($toolName) {
    $command = Get-Command $toolName -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin"
    )

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) {
            continue
        }

        $matches = Get-ChildItem -Path $root -Filter $toolName -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\$Architecture\\$toolName$" } |
            Sort-Object FullName -Descending
        if ($matches) {
            return $matches[0].FullName
        }
    }

    return $null
}

function Escape-Xml($value) {
    return [System.Security.SecurityElement]::Escape($value)
}

function New-ImageAsset($sourcePath, $targetPath, $width, $height) {
    Add-Type -AssemblyName System.Drawing

    $targetDir = Split-Path -Parent $targetPath
    New-Item -ItemType Directory -Force $targetDir | Out-Null

    $source = [System.Drawing.Image]::FromFile($sourcePath)
    try {
        $bitmap = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

                $padding = [Math]::Max(0, [Math]::Round([Math]::Min($width, $height) * 0.10))
                $maxWidth = [Math]::Max(1, $width - ($padding * 2))
                $maxHeight = [Math]::Max(1, $height - ($padding * 2))
                $scale = [Math]::Min($maxWidth / $source.Width, $maxHeight / $source.Height)
                $drawWidth = [Math]::Max(1, [Math]::Round($source.Width * $scale))
                $drawHeight = [Math]::Max(1, [Math]::Round($source.Height * $scale))
                $x = [Math]::Round(($width - $drawWidth) / 2)
                $y = [Math]::Round(($height - $drawHeight) / 2)

                $graphics.DrawImage($source, $x, $y, $drawWidth, $drawHeight)
                $bitmap.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)
            }
            finally {
                $graphics.Dispose()
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}

function Replace-Token($content, $token, $value) {
    return $content.Replace("{{" + $token + "}}", (Escape-Xml $value))
}

Write-Host "Building ClickLight..."
& (Join-Path $Root "build.ps1")

if (Test-Path $LayoutDir) {
    Remove-Item $LayoutDir -Recurse -Force
}
New-Item -ItemType Directory -Force $LayoutDir | Out-Null
New-Item -ItemType Directory -Force $OutRoot | Out-Null

Copy-Item -Path (Join-Path $Root "bin\ClickLight.exe") -Destination $LayoutDir -Force
Copy-Item -Path (Join-Path $Root "bin\assets") -Destination $LayoutDir -Recurse -Force

$assetDir = Join-Path $LayoutDir "Assets"
$logoSource = Join-Path $Root "assets\logo\clicklight-logo-clean-1024.png"
if (-not (Test-Path $logoSource)) {
    $logoSource = Join-Path $Root "assets\tray\clicklight-tray-256.png"
}

New-ImageAsset $logoSource (Join-Path $assetDir "StoreLogo.png") 50 50
New-ImageAsset $logoSource (Join-Path $assetDir "Square44x44Logo.png") 44 44
New-ImageAsset $logoSource (Join-Path $assetDir "Square150x150Logo.png") 150 150
New-ImageAsset $logoSource (Join-Path $assetDir "SmallTile.png") 71 71
New-ImageAsset $logoSource (Join-Path $assetDir "LargeTile.png") 310 310
New-ImageAsset $logoSource (Join-Path $assetDir "Wide310x150Logo.png") 310 150
New-ImageAsset $logoSource (Join-Path $assetDir "SplashScreen.png") 620 300

$manifest = Get-Content $ManifestTemplate -Raw
$manifest = Replace-Token $manifest "PACKAGE_NAME" $PackageName
$manifest = Replace-Token $manifest "PUBLISHER" $Publisher
$manifest = Replace-Token $manifest "VERSION" $Version
$manifest = Replace-Token $manifest "ARCHITECTURE" $Architecture
$manifest = Replace-Token $manifest "DISPLAY_NAME" $DisplayName
$manifest = Replace-Token $manifest "PUBLISHER_DISPLAY_NAME" $PublisherDisplayName
$manifest = Replace-Token $manifest "DESCRIPTION" $Description
$manifest | Set-Content -Path $ManifestOut -Encoding UTF8
[xml](Get-Content $ManifestOut -Raw) | Out-Null

Write-Host "Prepared MSIX layout: $LayoutDir"

if ($PrepareOnly) {
    Write-Host "PrepareOnly set; skipping MakeAppx/signing."
    exit 0
}

$makeAppx = Find-SdkTool "makeappx.exe"
if (-not $makeAppx) {
    throw "makeappx.exe was not found. Install the Windows SDK/MSIX packaging tools, then rerun this script. The layout is ready at: $LayoutDir"
}

if (Test-Path $PackagePath) {
    Remove-Item $PackagePath -Force
}

& $makeAppx pack /d $LayoutDir /p $PackagePath /overwrite
if ($LASTEXITCODE -ne 0) {
    throw "makeappx.exe failed with exit code $LASTEXITCODE"
}

$shouldSign = (-not $SkipSign) -and (($CertificateThumbprint -ne "") -or ($PfxPath -ne ""))
if ($shouldSign) {
    $signTool = Find-SdkTool "signtool.exe"
    if (-not $signTool) {
        throw "signtool.exe was not found. Install Windows SDK signing tools or rerun with -SkipSign."
    }

    $timestampArgs = @()
    if (-not $NoTimestamp) {
        $timestampArgs = @("/tr", "http://timestamp.digicert.com", "/td", "SHA256")
    }

    if ($CertificateThumbprint -ne "") {
        & $signTool sign /fd SHA256 /sha1 $CertificateThumbprint @timestampArgs $PackagePath
    }
    else {
        if ($PfxPassword -ne "") {
            & $signTool sign /fd SHA256 /f $PfxPath /p $PfxPassword @timestampArgs $PackagePath
        }
        else {
            & $signTool sign /fd SHA256 /f $PfxPath @timestampArgs $PackagePath
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "signtool.exe failed with exit code $LASTEXITCODE"
    }
}
else {
    Write-Warning "Package was created unsigned. Local install requires signing; Store submission must use Partner Center/package identity values."
}

Write-Host "Created MSIX: $PackagePath"
