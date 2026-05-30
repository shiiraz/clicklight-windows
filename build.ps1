$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceRoot = Join-Path $Root "src"
$Sources = Get-ChildItem -Path $SourceRoot -Filter *.cs -Recurse | Sort-Object FullName | ForEach-Object { $_.FullName }
$OutDir = Join-Path $Root "bin"
$OutFile = Join-Path $OutDir "ClickLight.exe"
$IconFile = Join-Path $Root "assets\tray\clicklight-tray.ico"

New-Item -ItemType Directory -Force $OutDir | Out-Null
if (Test-Path $OutFile) {
    Remove-Item $OutFile -Force
}

Add-Type -AssemblyName Microsoft.CSharp

$Provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$Parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$Parameters.GenerateExecutable = $true
$Parameters.OutputAssembly = $OutFile
$Parameters.CompilerOptions = "/target:winexe"
if (Test-Path $IconFile) {
    $Parameters.CompilerOptions += " /win32icon:`"$IconFile`""
}

@(
    "System.dll",
    "System.Core.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    "System.Xml.dll",
    "System.Runtime.Serialization.dll",
    "Microsoft.CSharp.dll"
) | ForEach-Object { [void]$Parameters.ReferencedAssemblies.Add($_) }

$Results = $Provider.CompileAssemblyFromFile($Parameters, [string[]]$Sources)
if ($Results.Errors.HasErrors) {
    foreach ($ErrorItem in $Results.Errors) {
        Write-Error $ErrorItem.ToString()
    }
    exit 1
}

$AssetSource = Join-Path $Root "assets"
if (Test-Path $AssetSource) {
    Copy-Item -Path $AssetSource -Destination $OutDir -Recurse -Force
}

Write-Host "Built $OutFile"
