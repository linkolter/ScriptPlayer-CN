param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$app = Join-Path $PSScriptRoot "..\ScriptPlayer\ScriptPlayer\bin\$Configuration"
$csc = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $vs = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
    $csc = Join-Path $vs 'MSBuild\Current\Bin\Roslyn\csc.exe'
}
& $csc /nologo /target:exe "/out:$app\TCodeWifiTests.exe" "/reference:$app\ScriptPlayer.Shared.dll" "$PSScriptRoot\TCodeWifiTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& "$app\TCodeWifiTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'TCode Wi-Fi regression tests failed.' }
$wpf = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\WPF"
& $csc /nologo /target:exe "/out:$app\TCodeWifiDialogTests.exe" "/reference:$app\ScriptPlayer.exe" "/reference:$wpf\PresentationFramework.dll" "/reference:$wpf\PresentationCore.dll" "/reference:$wpf\WindowsBase.dll" "$PSScriptRoot\TCodeWifiDialogTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Dialog test compilation failed.' }
& "$app\TCodeWifiDialogTests.exe" "$app\wifi-dialog.png"
if ($LASTEXITCODE -ne 0) { throw 'TCode Wi-Fi dialog tests failed.' }
