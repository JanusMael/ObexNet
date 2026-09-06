<#
.SYNOPSIS
    Publishes ObexNet as a self-contained, single-file, trimmed executable per RID and zips the result.

.DESCRIPTION
    The canonical publish entry point. The release workflow (.github/workflows/release.yml), the
    CI publish-check job and developers all run this same script, so a green CI publish and a
    green local publish exercise the same code.

    For each RID:
      1. Runs `dotnet publish -c Release -r <rid>`. The publish shape (SelfContained,
         PublishSingleFile, PublishTrimmed) lives in ObexNet/ObexNet.csproj, gated on
         publish-time evaluation, so nothing here repeats it. Output is tee'd to
         dist/logs/publish-<rid>.log.
      2. Scans the log for `warning IL<digits>` / `error IL<digits>` lines (ILLink trim
         diagnostics; TreatWarningsAsErrors promotes them to errors in this repository).
      3. Copies LICENSE and THIRD-PARTY-NOTICES.md into the staged folder (BSD-3-Clause
         clause 2 requires the upstream notice to ship with the binary) and zips it into
         dist/ObexNet-<rid>.zip.

    Archive names are unversioned on purpose: local builds and CI builds share one filename
    pattern, and the release tag carries the version.

    Exits non-zero if any RID fails to publish, so CI fails loudly instead of uploading a
    partial set of archives.

.PARAMETER Rids
    RIDs to publish. Defaults to both supported Windows RIDs. ObexNet drives the Win32 debug
    API, so only Windows RIDs exist; either can be produced from any host OS.

.PARAMETER Version
    Public version string stamped into the assembly by the Bennewitz.Ninja.AutoVersioning
    generator (passed as -p:PublicVersion). CI sets the PublicVersion environment variable
    from the release tag instead, which MSBuild picks up automatically; this parameter is the
    local override. When neither is present the generator falls back to its date-based build
    version.

.PARAMETER Clean
    Wipe dist/ and every bin/ and obj/ folder under the project directories before publishing.

.EXAMPLE
    pwsh publish/publish.ps1

.EXAMPLE
    pwsh publish/publish.ps1 -Rids win-x64 -Version 1.2.3

.EXAMPLE
    pwsh publish/publish.ps1 -Clean
#>

[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string[]] $Rids = @('win-x64', 'win-arm64'),

    [string] $Version,

    [switch] $Clean
)

$ErrorActionPreference = 'Stop'

# Suppress the cmdlet progress UI that Remove-Item -Recurse / Get-ChildItem -Recurse emit; its
# VT escape sequences garble IDE output panes and CI logs.
$ProgressPreference = 'SilentlyContinue'

# $PSScriptRoot is publish/ — the repo root is one level up.
$repoRoot    = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot 'ObexNet' 'ObexNet.csproj'
$projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
$distFolder  = Join-Path $repoRoot 'dist'
$logFolder   = Join-Path $distFolder 'logs'

# ── Optional clean ───────────────────────────────────────────────────────────
if ($Clean) {
    Write-Host 'Cleaning dist/ and bin/obj under every project...' -ForegroundColor Magenta
    if (Test-Path $distFolder) { Remove-Item -Recurse -Force -LiteralPath $distFolder }

    $projectDirs = Get-ChildItem -Path $repoRoot -Recurse -Filter '*.csproj' -File |
        Select-Object -ExpandProperty DirectoryName -Unique
    foreach ($projectDir in $projectDirs) {
        foreach ($name in 'bin', 'obj') {
            $dir = Join-Path $projectDir $name
            if (Test-Path $dir) { Remove-Item -Recurse -Force -LiteralPath $dir }
        }
    }
}

New-Item -ItemType Directory -Path $distFolder -Force | Out-Null
New-Item -ItemType Directory -Path $logFolder  -Force | Out-Null

$versionArgs = @()
if ($Version) { $versionArgs = @("-p:PublicVersion=$Version") }

Add-Type -AssemblyName 'System.IO.Compression.FileSystem'

$results = [System.Collections.Generic.List[object]]::new()

# ── Per-RID publish ──────────────────────────────────────────────────────────
foreach ($rid in $Rids) {
    Write-Host "`n>>> Publishing $projectName for $rid..." -ForegroundColor Yellow

    $staging = Join-Path $distFolder $rid
    $logPath = Join-Path $logFolder "publish-$rid.log"

    # Always start from an empty staging folder so leftovers from a previous run never end up
    # in the archive.
    if (Test-Path $staging) { Remove-Item -Recurse -Force -LiteralPath $staging }

    # Tee-Object does not touch $LASTEXITCODE, so the real `dotnet publish` exit code survives
    # the pipeline.
    dotnet publish $projectPath -c Release -r $rid --output $staging @versionArgs 2>&1 |
        Tee-Object -FilePath $logPath
    $exitCode = $LASTEXITCODE

    # ILLink trim diagnostics (IL2026, IL2072, ...). Both severities are matched: this repository
    # sets TreatWarningsAsErrors (Directory.Build.props), which promotes them to `error IL<digits>`,
    # so a warning-only pattern reports zero on precisely the publishes that failed because of them.
    $warningLines = @(Select-String -Path $logPath -Pattern '(warning|error) IL\d+')
    $warningCount = $warningLines.Count
    if ($warningCount -gt 0) {
        Write-Host ("[{0}] {1} ILLink trim diagnostic line(s) detected in {2}." -f $rid, $warningCount, (Split-Path $logPath -Leaf)) -ForegroundColor Yellow
    }

    $archiveName = '(none)'
    if ($exitCode -eq 0) {
        $archivePath = Join-Path $distFolder "$projectName-$rid.zip"
        if (Test-Path $archivePath) { Remove-Item -Force -LiteralPath $archivePath }

        # Clause 2 of the upstream BSD-3-Clause license (see THIRD-PARTY-NOTICES.md) requires the
        # copyright notice to travel with the binary distribution, so bundle both license files
        # into every archive alongside ObexNet.exe.
        foreach ($noticeFile in 'LICENSE', 'THIRD-PARTY-NOTICES.md') {
            $noticeSrc = Join-Path $repoRoot $noticeFile
            if (Test-Path $noticeSrc) {
                Copy-Item -LiteralPath $noticeSrc -Destination (Join-Path $staging $noticeFile) -Force
            }
            else {
                Write-Host "[$rid] WARNING: $noticeFile not found at repo root; archive will omit it." -ForegroundColor Yellow
            }
        }

        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $staging,
            $archivePath,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $false)

        $fileCount   = @(Get-ChildItem -Path $staging -Recurse -File).Count
        $archiveName = Split-Path $archivePath -Leaf
        Write-Host "[$rid] Created $archiveName ($fileCount file(s))" -ForegroundColor Cyan

        Remove-Item -Recurse -Force -LiteralPath $staging
    }
    else {
        Write-Host "[$rid] dotnet publish exited with code $exitCode." -ForegroundColor Red
    }

    $results.Add([pscustomobject]@{
        Rid          = $rid
        ExitCode     = $exitCode
        WarningCount = $warningCount
        Archive      = $archiveName
    })
}

# ── Summary ──────────────────────────────────────────────────────────────────
Write-Host "`nResults:" -ForegroundColor Green
$results | Format-Table Rid, ExitCode, WarningCount, Archive | Out-Host
Write-Host "Output zips:  $distFolder"
Write-Host "Publish logs: $logFolder"

$failed = @($results | Where-Object { $_.ExitCode -ne 0 })
if ($failed.Count -gt 0) {
    Write-Host ('Publish FAILED for: {0}' -f (($failed | ForEach-Object Rid) -join ', ')) -ForegroundColor Red
    exit 1
}

Write-Host 'Publish complete.' -ForegroundColor Green
