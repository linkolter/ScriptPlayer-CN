# Download the tested components from their original distributors, without overwriting existing files.
[CmdletBinding()]
param([string]$SevenZip, [string]$ArchiveDirectory)
$ErrorActionPreference = 'Stop'
try {
    $app = Join-Path $PSScriptRoot 'ScriptPlayer.exe'
    if (-not [IO.File]::Exists($app)) { throw 'Extract the complete application ZIP before running this script.' }
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-downloads.json') -Raw | ConvertFrom-Json
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($app).FileVersion -ne $manifest.ApplicationVersion) { throw 'Application version does not match the runtime manifest.' }
    if (-not $SevenZip) {
        $candidates = @()
        if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles '7-Zip\7z.exe' }
        if (${env:ProgramFiles(x86)}) { $candidates += Join-Path ${env:ProgramFiles(x86)} '7-Zip\7z.exe' }
        $command = Get-Command '7z.exe' -ErrorAction SilentlyContinue
        if ($command) { $candidates += $command.Source }
        $SevenZip = $candidates | Where-Object { [IO.File]::Exists($_) } | Select-Object -First 1
    }
    Import-Module (Join-Path $PSScriptRoot 'RuntimeInstaller.psm1') -Force
    $result = Install-PinnedRuntimes -TargetDirectory $PSScriptRoot -Components $manifest.Components -SevenZip ([string]$SevenZip) -ArchiveDirectory $ArchiveDirectory
    Write-Host ($result.Status + '. Start ScriptPlayer.exe.')
    exit 0
} catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
