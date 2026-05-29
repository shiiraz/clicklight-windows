$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$SourceRoot = Join-Path $Root "src"
$TestRoot = Join-Path $Root "tests"
$OutDir = Join-Path $TestRoot "bin"
$OutFile = Join-Path $OutDir "ClickLight.Tests.exe"

$Sources = Get-ChildItem -Path $SourceRoot -Filter *.cs -Recurse |
    Where-Object { $_.Name -ne "Program.cs" } |
    Sort-Object FullName |
    ForEach-Object { $_.FullName }

$Sources += Join-Path $TestRoot "TestHarness.cs"

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
    -OutputType ConsoleApplication

& $OutFile
