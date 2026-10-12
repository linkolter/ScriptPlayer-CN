param([switch]$Baseline, [switch]$ExpectRed, [string]$SourcePath)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidence = Join-Path $repository '.scratch\round3-r02\evidence'
$run = Join-Path $evidence ('nfo-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
if (Test-Path -LiteralPath $run) { throw 'Refusing to overwrite an existing evidence run.' }
[IO.Directory]::CreateDirectory($run) | Out-Null
$fixtures = Join-Path $run 'fixtures'
[IO.Directory]::CreateDirectory($fixtures) | Out-Null
[IO.File]::WriteAllText((Join-Path $fixtures '.scriptplayer-r02-private-fixture'), 'R02 private test fixtures')
$compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Independent test compiler unavailable.' }
if (-not $SourcePath) {
    $SourcePath = if ($Baseline) {
        Join-Path $repository '.scratch\round3-r02\before\ScriptPlayer\ScriptPlayer\Metadata\NfoMetadata.cs'
    } else { Join-Path $repository 'ScriptPlayer\ScriptPlayer\Metadata\NfoMetadata.cs' }
}
$SourcePath = [IO.Path]::GetFullPath($SourcePath)
if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) { throw 'NfoMetadata source unavailable.' }
$executable = Join-Path $run 'NfoMetadataTests.exe'
$log = Join-Path $run $(if ($ExpectRed) { 'nfo-baseline.red.log' } else { 'nfo-metadata.log' })
$testSource = Join-Path $PSScriptRoot 'NfoMetadataTests.cs'
$compileArguments = @('/nologo', '/target:exe', '/reference:System.Core.dll', '/reference:System.Xml.dll', "/out:$executable", $testSource, $SourcePath)
if (-not $Baseline) { $compileArguments += '/define:NFO_COMMIT_SEAM' }
& $compiler @compileArguments 2>&1 | Tee-Object -FilePath (Join-Path $run 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Independent NFO test compilation failed.' }
& $executable $fixtures 2>&1 | Tee-Object -FilePath $log
$testExit = $LASTEXITCODE
$manifest = [ordered]@{
    Completed=(Get-Date -Format o); Baseline=[bool]$Baseline; ExpectedRed=[bool]$ExpectRed
    Scope='Pure NfoMetadata Load/Save; only runner-created private fixtures; no WPF, APPDATA, real media, or devices.'
    Sources=@($SourcePath,$testSource,$PSCommandPath | ForEach-Object { [ordered]@{Path=$_;SHA256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash} })
    Compiler=$compiler;ExecutableSHA256=(Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    LogSHA256=(Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash
    Passed=@(Get-Content -LiteralPath $log | Where-Object {$_ -like 'PASS:*'}).Count
    Failed=@(Get-Content -LiteralPath $log | Where-Object {$_ -like 'FAIL:*'}).Count
    ExitCode=$testExit
    Boundary='Pure XML/physical-commit suite; actual WPF Entry publication is covered separately by Run-NfoMetadataIntegrationTests.ps1.'
}
[IO.File]::WriteAllText((Join-Path $run 'verification.json'), ($manifest | ConvertTo-Json -Depth 5))
Write-Output "Evidence: $run"
if ($ExpectRed) {
    if ($testExit -ne 1 -or $manifest.Failed -eq 0) { throw 'Baseline did not reproduce assertion failures as expected.' }
    Write-Output "Expected baseline failure captured: $log"
} elseif ($testExit -ne 0) { throw "NFO regression failed: $log" }
