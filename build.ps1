$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceRoot = Join-Path $Root "src"
$Sources = Get-ChildItem -Path $SourceRoot -Filter *.cs -Recurse | Sort-Object FullName | ForEach-Object { $_.FullName }
$OutDir = Join-Path $Root "bin"
$OutFile = Join-Path $OutDir "ClickLight.exe"

New-Item -ItemType Directory -Force $OutDir | Out-Null
if (Test-Path $OutFile) {
    Remove-Item $OutFile -Force
}

Add-Type `
    -Path $Sources `
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
