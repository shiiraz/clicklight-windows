$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Source = Join-Path $Root "src\ClickLight.cs"
$OutDir = Join-Path $Root "bin"
$OutFile = Join-Path $OutDir "ClickLight.exe"

New-Item -ItemType Directory -Force $OutDir | Out-Null
if (Test-Path $OutFile) {
    Remove-Item $OutFile -Force
}

Add-Type `
    -Path $Source `
    -ReferencedAssemblies @(
        "System.dll",
        "System.Core.dll",
        "System.Drawing.dll",
        "System.Windows.Forms.dll",
        "System.Xml.dll",
        "System.Runtime.Serialization.dll",
        "Microsoft.CSharp.dll"
    ) `
    -OutputAssembly $OutFile `
    -OutputType WindowsApplication

Write-Host "Built $OutFile"
