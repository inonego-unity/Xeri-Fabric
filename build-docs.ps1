param(
    [switch]$Serve,
    [switch]$SkipMetadata,
    [switch]$VerifySnapshot,
    [string]$UnityProjectRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = $PSScriptRoot
$packageRoot = Join-Path $repoRoot 'com.inonego.xeri.fabric'
$apiDir = Join-Path $repoRoot '.docfx/api'
$metadataProjectDir = Join-Path $repoRoot '.docfx/metadata-project'
$snapshotPath = Join-Path $repoRoot 'DocsSite/generated-api.zip'
$snapshotHashPath = Join-Path $repoRoot 'DocsSite/generated-api.sha256'
$siteDir = Join-Path $repoRoot '_site'

if ([string]::IsNullOrWhiteSpace($UnityProjectRoot))
{
    $UnityProjectRoot = Join-Path (Split-Path $repoRoot -Parent) 'KnackH'
}
$unityProjectRoot = [System.IO.Path]::GetFullPath($UnityProjectRoot)

. (Join-Path $repoRoot 'DocsSite/api-snapshot.ps1')

if ($VerifySnapshot)
{
    Assert-ApiSnapshotCurrent
    Write-Host 'API snapshot is current.'
    exit 0
}
Push-Location $repoRoot
try
{
    dotnet tool restore
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet tool restore failed with exit code $LASTEXITCODE."
    }

    if (-not $SkipMetadata)
    {
        if (Test-Path $apiDir)
        {
            Remove-Item $apiDir -Recurse -Force
        }

        Write-Host 'Generating DocFX metadata projects...'
        $projects = @(New-DocFxMetadataProjects)
        foreach ($project in $projects)
        {
            dotnet restore $project --nologo
            if ($LASTEXITCODE -ne 0)
            {
                throw "dotnet restore failed for $project with exit code $LASTEXITCODE."
            }
        }

        Write-Host 'Generating DocFX API metadata...'
        dotnet docfx metadata docfx.json
        if ($LASTEXITCODE -ne 0)
        {
            throw "DocFX metadata failed with exit code $LASTEXITCODE."
        }

        Write-Host 'Writing sanitized API snapshot...'
        Write-ApiSnapshot
        Write-Host 'API snapshot updated.'
        Restore-ApiSnapshot
    }
    elseif (-not (Test-Path (Join-Path $apiDir 'toc.yml')))
    {
        Restore-ApiSnapshot
    }

    if (Test-Path $siteDir)
    {
        Remove-Item $siteDir -Recurse -Force
    }

    $buildArgs = @('docfx', 'build', 'docfx.json')
    if ($Serve)
    {
        $buildArgs += '--serve'
    }

    Write-Host 'Building DocFX site...'
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0)
    {
        throw "DocFX build failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Pop-Location
}