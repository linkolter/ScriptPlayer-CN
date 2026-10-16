param([string]$PublishName = '', [switch]$PublicRelease)
$workspace = & (Join-Path $PSScriptRoot '../../tests/Find-LocalWorkspace.ps1')
if ([string]::IsNullOrWhiteSpace($PublishName)) { $PublishName = 'playback-baseline/builds/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,6) }
$ErrorActionPreference = 'Stop'
$projectRoot = Join-Path $workspace 'github-upload\ScriptPlayer-CN-20261012\ScriptPlayer\ScriptPlayer'
$referenceRoot = Join-Path $workspace 'net48-reference\build'
$buildTool = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
if (!(Test-Path -LiteralPath $buildTool)) { throw '需要 Visual Studio 2022 的 MSBuild。' }
foreach ($runtimeFile in @('ffmpeg.exe','libmpv-2.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $projectRoot "Runtime\$runtimeFile"))) { throw "缺少随附组件：$runtimeFile" }
}
$publicOptions = @()
if ($PublicRelease) { $publicOptions = @('/t:Rebuild', '/p:DebugType=none', '/p:DebugSymbols=false') }
& $buildTool (Join-Path $projectRoot 'ScriptPlayer.csproj') /p:Configuration=Release /p:UsePrebuiltProjectReferences=true "/p:TargetFrameworkRootPath=$referenceRoot\" @publicOptions /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "编译失败：$LASTEXITCODE" }
$appBin = Join-Path $projectRoot 'bin\Release'
$publishDir = Join-Path $workspace $PublishName
if (Test-Path -LiteralPath $publishDir) { throw 'Choose a new publish directory; existing output is preserved.' }
New-Item -ItemType Directory $publishDir | Out-Null
Get-ChildItem -LiteralPath $appBin -File |
    Where-Object { $_.Extension -in '.dll','.exe','.config' -and $_.Name -notmatch 'Tests' } |
    Copy-Item -Destination $publishDir
$runtimeIdentity = Join-Path $appBin 'runtime-versions.json'
if (Test-Path -LiteralPath $runtimeIdentity) {
    Copy-Item -LiteralPath $runtimeIdentity -Destination $publishDir
    $runtimeManifest = Get-Content -LiteralPath $runtimeIdentity -Raw | ConvertFrom-Json
    foreach ($component in $runtimeManifest.Files) {
        if ([IO.Path]::GetFileName($component.File) -ne $component.File) { throw 'Invalid runtime manifest filename.' }
        $publishedFile = Join-Path $publishDir $component.File
        if (!(Test-Path -LiteralPath $publishedFile) -or (Get-Item -LiteralPath $publishedFile).Length -ne $component.Bytes -or
            (Get-FileHash -LiteralPath $publishedFile -Algorithm SHA256).Hash -ne $component.SHA256) {
            throw ('Published runtime identity mismatch: ' + $component.File)
        }
    }
}
foreach ($assetFolder in @('Licenses','x64','x86')) {
    Copy-Item -LiteralPath (Join-Path $appBin $assetFolder) -Destination $publishDir -Recurse -Force
}
Write-Output "已生成：$publishDir\ScriptPlayer.exe"
