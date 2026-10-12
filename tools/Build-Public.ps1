param([string]$MSBuildPath)
$ErrorActionPreference='Stop'
$project=Join-Path (Split-Path $PSScriptRoot -Parent) 'ScriptPlayer/ScriptPlayer/ScriptPlayer.csproj'
if (!$MSBuildPath) {
    $locator=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (!(Test-Path -LiteralPath $locator)) { throw 'Install Visual Studio 2022 or pass MSBuildPath.' }
    $MSBuildPath=(& $locator -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1)
}
if (!(Test-Path -LiteralPath $MSBuildPath)) { throw 'MSBuild is unavailable.' }
& $MSBuildPath $project /t:Rebuild /p:Configuration=Release /p:DebugType=none /p:DebugSymbols=false /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed; restore required dependencies and review diagnostics.' }
