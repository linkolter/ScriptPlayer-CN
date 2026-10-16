param([Parameter(Mandatory=$true)][string]$PrivateBin,
      [Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$workspace=& (Join-Path $PSScriptRoot 'Find-LocalWorkspace.ps1')
$project=Split-Path $PSScriptRoot -Parent
$bin=(Resolve-Path -LiteralPath (Join-Path $workspace $PrivateBin)).Path
$output=[IO.Path]::GetFullPath((Join-Path $project $OutputDirectory))
if (!$output.StartsWith($project+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Output must remain inside the project.' }
if (Test-Path -LiteralPath $output) { throw 'Use a new fixture output directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$inputFile=Join-Path $output '测试视频 with spaces.mp4'
& (Join-Path $bin 'ffmpeg.exe') -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=640x360:rate=24' -f lavfi -i 'sine=frequency=440:sample_rate=48000' -t 3 -c:v libx264 -pix_fmt yuv420p -c:a aac $inputFile *> (Join-Path $output 'fixture-generation.log')
if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed.' }
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
& $compiler /nologo /target:exe "/r:$bin\ScriptPlayer.Shared.dll" /r:System.Core.dll "/out:$bin\RuntimeCompatibilityTests.exe" (Join-Path $PSScriptRoot 'RuntimeCompatibilityTests.cs') *> (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Compatibility harness compilation failed.' }
& (Join-Path $bin 'RuntimeCompatibilityTests.exe') $inputFile (Join-Path $output 'results') *> (Join-Path $output 'results.log')
$testExit=$LASTEXITCODE
Get-Content (Join-Path $output 'results.log')
if ($testExit -ne 0) { throw 'Runtime compatibility tests failed.' }
