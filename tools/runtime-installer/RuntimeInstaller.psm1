Set-StrictMode -Version Latest

function Test-PinnedFile {
    param([string]$Path, [long]$Bytes, [string]$SHA256)
    if (-not [IO.File]::Exists($Path)) { return $false }
    if ((Get-Item -LiteralPath $Path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { return $false }
    if ((Get-Item -LiteralPath $Path -Force).Length -ne $Bytes) { return $false }
    $stream = [IO.File]::OpenRead($Path)
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $actual = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '') }
    finally { $hasher.Dispose(); $stream.Dispose() }
    return $actual -eq $SHA256
}

function Install-PinnedRuntimes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TargetDirectory,
        [Parameter(Mandatory)][object[]]$Components,
        [Parameter(Mandatory)][AllowEmptyString()][string]$SevenZip,
        [string]$ArchiveDirectory,
        # Used only by failure tests; normal installs use non-overwriting File.Move.
        [scriptblock]$MoveFile = { param($From, $To) [IO.File]::Move($From, $To) }
    )
    $ErrorActionPreference = 'Stop'
    $target = (Resolve-Path -LiteralPath $TargetDirectory).ProviderPath
    $targetItem = Get-Item -LiteralPath $target -Force
    if (-not $targetItem.PSIsContainer -or ($targetItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Target must be an existing ordinary directory.'
    }
    $pending = [Collections.Generic.List[object]]::new()
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($component in $Components) {
        if ($component.Name -notin @('mpv','ffmpeg') -or $component.File -notin @('libmpv-2.dll', 'ffmpeg.exe') -or -not $names.Add($component.File)) {
            throw 'Invalid or duplicate runtime target.'
        }
        $url = [Uri]$component.URL
        if ($url.Scheme -ne 'https' -or $url.Host -ne 'github.com' -or $url.Query -or $url.Fragment -or $url.UserInfo -or
            $url.AbsolutePath -notmatch '^/(shinchiro/mpv-winbuild-cmake|GyanD/codexffmpeg)/releases/download/[^/]+/[^/]+\.7z$') {
            throw 'Runtime URL is outside the original distributors.'
        }
        if ([IO.Path]::GetFileName($url.AbsolutePath) -ne $component.Archive -or
            [IO.Path]::GetFileName($component.Archive) -ne $component.Archive -or $component.Archive -notmatch '^[a-zA-Z0-9_.-]+\.7z$' -or
            $component.SHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or $component.ArchiveSHA256 -notmatch '^[A-Fa-f0-9]{64}$' -or
            $component.Bytes -le 0 -or $component.ArchiveBytes -le 0 -or
            $component.Member -notmatch '^[a-zA-Z0-9_.-]+(/[a-zA-Z0-9_.-]+)*$' -or
            $component.Member.Split('/') -contains '..' -or [IO.Path]::GetFileName($component.Member) -ne $component.File) {
            throw 'Invalid pinned runtime manifest.'
        }
        $destination = Join-Path $target $component.File
        if (Test-Path -LiteralPath $destination) {
            if (-not (Test-PinnedFile $destination $component.Bytes $component.SHA256)) {
                throw ('Existing runtime differs; nothing was overwritten: ' + $component.File)
            }
        } else { $pending.Add($component) }
    }
    if ($pending.Count -eq 0) { return [pscustomobject]@{ Status='AlreadyInstalled'; Installed=0 } }
    if (-not [IO.File]::Exists($SevenZip)) { throw '7-Zip is required. Install it from https://www.7-zip.org/ or provide -SevenZip.' }
    if ($ArchiveDirectory -and -not (Test-Path -LiteralPath $ArchiveDirectory -PathType Container)) { throw 'ArchiveDirectory does not exist.' }

    $stage = Join-Path $target ('.runtime-install-' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($stage) | Out-Null
    $installed = [Collections.Generic.List[object]]::new()
    $retainStage = $false
    $oldTls = [Net.ServicePointManager]::SecurityProtocol
    try {
        [Net.ServicePointManager]::SecurityProtocol = $oldTls -bor [Net.SecurityProtocolType]::Tls12
        foreach ($component in $pending) {
            Write-Host ('Preparing ' + $component.Name + ' from original distributor...')
            $archive = Join-Path $stage $component.Archive
            if ($ArchiveDirectory) {
                $cached = Join-Path $ArchiveDirectory $component.Archive
                if (-not (Test-PinnedFile $cached $component.ArchiveBytes $component.ArchiveSHA256)) { throw ('Cached archive mismatch: ' + $component.Archive) }
                [IO.File]::Copy($cached, $archive, $false)
            } else {
                $client = [Net.WebClient]::new()
                try {
                    $client.Headers['User-Agent'] = 'ScriptPlayer-runtime-installer/1.0.12.3'
                    $client.DownloadFile($component.URL, $archive)
                } finally { $client.Dispose() }
            }
            if (-not (Test-PinnedFile $archive $component.ArchiveBytes $component.ArchiveSHA256)) { throw ('Archive SHA256/length mismatch: ' + $component.Archive) }
            $extract = Join-Path $stage $component.Name
            [IO.Directory]::CreateDirectory($extract) | Out-Null
            # Extract only the pinned member, never execute content from the archive.
            $sevenZipOutput = & $SevenZip e $archive ('-o' + $extract) $component.Member '-aoa' '-y' '-bb0' 2>&1
            if ($LASTEXITCODE -ne 0) { throw ('7-Zip extraction failed for ' + $component.Name) }
            $source = Join-Path $extract $component.File
            if (-not (Test-PinnedFile $source $component.Bytes $component.SHA256)) { throw ('Extracted runtime SHA256/length mismatch: ' + $component.File) }
            $component | Add-Member -NotePropertyName StagedFile -NotePropertyValue $source -Force
        }
        # Both downloads and extracted files passed before any target path changes.
        foreach ($component in $pending) {
            $destination = Join-Path $target $component.File
            if (Test-Path -LiteralPath $destination) { throw ('Target appeared during installation: ' + $component.File) }
            & $MoveFile $component.StagedFile $destination
            $installed.Add($component)
        }
        return [pscustomobject]@{ Status='Installed'; Installed=$installed.Count }
    } catch {
        $originalError = $_.Exception.Message
        $failures = [Collections.Generic.List[string]]::new()
        for ($index=$installed.Count-1; $index -ge 0; $index--) {
            $component = $installed[$index]
            $destination = Join-Path $target $component.File
            try {
                if (-not (Test-PinnedFile $destination $component.Bytes $component.SHA256)) { throw 'Runtime changed or disappeared after installation.' }
                & $MoveFile $destination $component.StagedFile
            } catch { $failures.Add($component.File + ': ' + $_.Exception.Message) }
        }
        if ($failures.Count -gt 0) {
            $retainStage = $true
            $record = [ordered]@{ OriginalError=$originalError; RollbackFailures=@($failures);
                CurrentTargets=@($pending | ForEach-Object { [pscustomobject]@{File=$_.File; Exists=[IO.File]::Exists((Join-Path $target $_.File)); SHA256=$_.SHA256} }) }
            try { $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $stage 'recovery.json') -Encoding UTF8 }
            catch { throw ('Installation failed and rollback is incomplete; recovery.json could not be written. Keep ' + [IO.Path]::GetFileName($stage) + '. ' + $originalError) }
            throw ('Installation failed and rollback is incomplete. Keep ' + [IO.Path]::GetFileName($stage) + '/recovery.json. ' + $originalError)
        }
        throw ('Installation failed; newly installed files were rolled back. ' + $originalError)
    } finally {
        [Net.ServicePointManager]::SecurityProtocol = $oldTls
        if (-not $retainStage) {
            $absoluteStage = [IO.Path]::GetFullPath($stage)
            $prefix = $target.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
            if (-not $absoluteStage.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
                [IO.Path]::GetFileName($absoluteStage) -notmatch '^\.runtime-install-[a-f0-9]{32}$') { throw 'Unsafe temporary cleanup path.' }
            try { [IO.Directory]::Delete($absoluteStage, $true) } catch { Write-Warning ('Temporary files remain in ' + [IO.Path]::GetFileName($stage)) }
        }
    }
}
Export-ModuleMember -Function Install-PinnedRuntimes, Test-PinnedFile
