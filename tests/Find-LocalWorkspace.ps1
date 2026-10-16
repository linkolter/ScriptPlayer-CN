$probe = Get-Item -LiteralPath (Split-Path $PSScriptRoot -Parent)
while ($probe) {
    if ((Test-Path -LiteralPath (Join-Path $probe.FullName 'net48-reference')) -and (Test-Path -LiteralPath (Join-Path $probe.FullName 'run-playback-baseline.ps1'))) { return $probe.FullName }
    $probe = $probe.Parent
}
throw 'Local regression workspace not found. Configure the net48 reference/test harness workspace first.'
