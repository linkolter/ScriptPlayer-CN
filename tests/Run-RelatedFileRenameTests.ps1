param([switch]$LegacyRed, [string]$EvidenceDirectory='.scratch/round1-r03-b01/evidence')
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidence = [IO.Path]::GetFullPath((Join-Path $repository $EvidenceDirectory))
if (!$evidence.StartsWith($repository + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence directory must stay in project' }
[IO.Directory]::CreateDirectory($evidence) | Out-Null
$compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Roslyn compiler unavailable; no production build attempted.' }
$prefix = if ($LegacyRed) { 'r03-legacy-red' } else { 'r03-module' }
$executable = Join-Path $evidence ($prefix + '.exe')
$log = Join-Path $evidence ($prefix + '.log')
$arguments = @('/nologo', '/target:exe', '/reference:System.Core.dll', '/reference:System.Web.Extensions.dll', "/out:$executable", (Join-Path $PSScriptRoot 'RelatedFileRenameTests.cs'))
if ($LegacyRed) {
    $arguments += '/define:LEGACY_RED'
    $source = Join-Path $repository 'ScriptPlayer\ScriptPlayer\ViewModels\PlaylistViewModel.cs'
    $record = Join-Path $evidence 'r03-legacy-source.txt'
    if (-not (Test-Path -LiteralPath $record)) {
        $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        [IO.File]::WriteAllText($record, "Source: $source`r`nSHA256: $hash`r`nCopied method: GetNewName lines 863-868; initial red run before production caller edits.`r`n")
    }
} else { $arguments += Join-Path $repository 'ScriptPlayer\ScriptPlayer\Library\RelatedFileRename.cs' }
& $compiler @arguments 2>&1 | Tee-Object -FilePath (Join-Path $evidence ($prefix + '-compile.log'))
if ($LASTEXITCODE -ne 0) { throw 'Independent R03 test compilation failed.' }
& $executable 2>&1 | Tee-Object -FilePath $log
$testExit = $LASTEXITCODE
if ($LegacyRed) {
    if ($testExit -eq 0) { throw 'Legacy red reproduction unexpectedly passed.' }
    Write-Output "Expected legacy failure captured: $log"
} elseif ($testExit -ne 0) { throw "R03 module regression failed: $log" }
if (-not $LegacyRed) {
    $r03Sources = @((Join-Path $repository 'ScriptPlayer\ScriptPlayer\Library\RelatedFileRename.cs'), (Join-Path $PSScriptRoot 'RelatedFileRenameTests.cs'), $PSCommandPath)
    $r03Verification = [ordered]@{
        Completed = (Get-Date -Format o)
        Passed = @(Get-Content -LiteralPath $log | Where-Object { $_ -like 'PASS:*' }).Count
        Failed = @(Get-Content -LiteralPath $log | Where-Object { $_ -like 'FAIL:*' }).Count
        Compiler = $compiler
        Scope = 'Independent module tests; private temporary trees; no UI, production rebuild, user media, or devices.'
        Limitation = 'No durable per-move crash journal. Manifest is a diagnostic snapshot for trusted local recovery.'
        Sources = @($r03Sources | ForEach-Object { [ordered]@{ Path = $_; SHA256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } })
        ExecutableSHA256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
        LogSHA256 = (Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash
    }
    [IO.File]::WriteAllText((Join-Path $evidence 'r03-module-verification.json'), ($r03Verification | ConvertTo-Json -Depth 5))
}
