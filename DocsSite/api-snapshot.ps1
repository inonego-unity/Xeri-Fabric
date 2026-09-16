Set-StrictMode -Version Latest

if (-not ('System.IO.Compression.ZipFile' -as [type]))
{
    Add-Type -AssemblyName System.IO.Compression.FileSystem
}

function Get-StringSha256([string]$value)
{
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try
    {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($value)
        return (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '')
    }
    finally
    {
        $sha.Dispose()
    }
}

function Test-ExcludedApiPath([string]$path)
{
    $segments = $path.Replace([char]47, [char]92).Split([char]92)
    return @($segments | Where-Object { $_ -ieq 'TEST' -or $_ -ieq 'Tests' }).Count -gt 0
}

function Get-ProductionApiFiles
{
    $roots = @('Runtime', 'Editor') |
        ForEach-Object { Join-Path $packageRoot $_ } |
        Where-Object { Test-Path $_ }

    return @(
        foreach ($root in $roots)
        {
            Get-ChildItem $root -Recurse -File |
                Where-Object {
                    $_.Extension -in @('.cs', '.asmdef', '.asmref') -and
                    -not (Test-ExcludedApiPath $_.FullName)
                }
        }
    )
}
function Get-ApiSourceHash
{
    $files = @(Get-ProductionApiFiles)
    $files += Get-Item (Join-Path $packageRoot 'package.json')

    $entries = foreach ($file in $files)
    {
        $text = [System.IO.File]::ReadAllText($file.FullName)
        $normalized = $text.Replace("`r`n", "`n").Replace("`r", "`n")
        $rootPrefix = $repoRoot.TrimEnd([char]92, [char]47)
        $relative = $file.FullName.Substring($rootPrefix.Length).TrimStart([char]92, [char]47)
        $relative = $relative.Replace([char]92, [char]47)
        "$relative`n$(Get-StringSha256 $normalized)"
    }

    return Get-StringSha256 (($entries | Sort-Object) -join "`n")
}

function Get-ProductionAssemblyDefinitions
{
    foreach ($file in (Get-ChildItem $packageRoot -Recurse -Filter '*.asmdef' -File))
    {
        if (Test-ExcludedApiPath $file.FullName)
        {
            continue
        }

        $json = Get-Content $file.FullName -Raw | ConvertFrom-Json
        [PSCustomObject]@{
            Name = $json.name
            Path = $file.FullName
            References = @($json.references)
        }
    }
}
function Resolve-SourceAssembly([System.IO.FileInfo]$file)
{
    $directory = $file.Directory
    while ($null -ne $directory)
    {
        if (-not $directory.FullName.StartsWith(
            $packageRoot,
            [System.StringComparison]::OrdinalIgnoreCase))
        {
            break
        }

        $asmref = Get-ChildItem $directory.FullName -Filter '*.asmref' -File |
            Select-Object -First 1
        if ($null -ne $asmref)
        {
            return (Get-Content $asmref.FullName -Raw | ConvertFrom-Json).reference
        }

        $asmdef = Get-ChildItem $directory.FullName -Filter '*.asmdef' -File |
            Select-Object -First 1
        if ($null -ne $asmdef)
        {
            return (Get-Content $asmdef.FullName -Raw | ConvertFrom-Json).name
        }

        if ($directory.FullName -eq $packageRoot)
        {
            break
        }
        $directory = $directory.Parent
    }

    return $null
}
function New-DocFxMetadataProjects
{
    if (-not (Test-Path $unityProjectRoot))
    {
        throw "Unity project root not found: $unityProjectRoot"
    }

    $templateProject = Join-Path $unityProjectRoot 'inonego.Xeri.csproj'
    $baseAssembly = Join-Path $unityProjectRoot 'Library/ScriptAssemblies/inonego.Xeri.dll'
    if (-not (Test-Path $templateProject))
    {
        throw "Unity-generated template project not found: $templateProject"
    }
    if (-not (Test-Path $baseAssembly))
    {
        throw "Compiled Xeri assembly not found: $baseAssembly"
    }

    if (Test-Path $metadataProjectDir)
    {
        Remove-Item $metadataProjectDir -Recurse -Force
    }
    New-Item $metadataProjectDir -ItemType Directory -Force | Out-Null

    $definitions = @(Get-ProductionAssemblyDefinitions)
    if ($definitions.Count -eq 0)
    {
        throw 'No production asmdef files were found.'
    }

    $definitionNames = @($definitions | ForEach-Object { $_.Name })
    $sourcesByAssembly = @{}
    foreach ($name in $definitionNames)
    {
        $sourcesByAssembly[$name] = [System.Collections.Generic.List[string]]::new()
    }


    foreach ($file in (Get-ProductionApiFiles | Where-Object Extension -eq '.cs'))
    {
        $assemblyName = Resolve-SourceAssembly $file
        if ($null -ne $assemblyName -and $sourcesByAssembly.ContainsKey($assemblyName))
        {
            $sourcesByAssembly[$assemblyName].Add($file.FullName)
        }
    }

    $projectPaths = @{}
    foreach ($definition in $definitions)
    {
        $safeName = $definition.Name -replace '[^A-Za-z0-9_.-]', '_'
        $projectPaths[$definition.Name] = Join-Path $metadataProjectDir "$safeName.DocFX.csproj"
    }

    foreach ($definition in $definitions)
    {
        [xml]$project = [System.IO.File]::ReadAllText($templateProject)
        foreach ($node in @($project.SelectNodes('//AssemblyName')))
        {
            $node.InnerText = $definition.Name
        }

        foreach ($hintPath in @($project.SelectNodes('//HintPath')))
        {
            if (-not [System.IO.Path]::IsPathRooted($hintPath.InnerText))
            {
                $hintPath.InnerText = [System.IO.Path]::GetFullPath(
                    (Join-Path $unityProjectRoot $hintPath.InnerText))
            }
        }

        foreach ($itemGroup in @($project.Project.ItemGroup))
        {
            foreach ($node in @($itemGroup.ChildNodes))
            {
                if ($node.Name -in @('Compile', 'None', 'ProjectReference', 'Analyzer'))
                {
                    [void]$itemGroup.RemoveChild($node)
                }
            }
        }

        $compileGroup = $project.CreateElement('ItemGroup')
        [void]$project.Project.AppendChild($compileGroup)
        foreach ($sourcePath in $sourcesByAssembly[$definition.Name])
        {
            $compile = $project.CreateElement('Compile')
            $compile.SetAttribute('Include', $sourcePath)
            [void]$compileGroup.AppendChild($compile)
        }

        foreach ($referenceName in $definition.References)
        {
            if ($projectPaths.ContainsKey($referenceName))
            {
                $projectReference = $project.CreateElement('ProjectReference')
                $projectReference.SetAttribute('Include', $projectPaths[$referenceName])
                [void]$compileGroup.AppendChild($projectReference)
                continue
            }

            if ($referenceName -eq 'inonego.Xeri')
            {
                $reference = $project.CreateElement('Reference')
                $reference.SetAttribute('Include', 'inonego.Xeri')
                $hintPath = $project.CreateElement('HintPath')
                $hintPath.InnerText = $baseAssembly
                [void]$reference.AppendChild($hintPath)
                $private = $project.CreateElement('Private')
                $private.InnerText = 'False'
                [void]$reference.AppendChild($private)
                [void]$compileGroup.AppendChild($reference)
            }
        }

        $settings = [System.Xml.XmlWriterSettings]::new()
        $settings.Indent = $true
        $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
        $writer = [System.Xml.XmlWriter]::Create($projectPaths[$definition.Name], $settings)
        try
        {
            $project.Save($writer)
        }
        finally
        {
            $writer.Dispose()
        }
    }

    return @($projectPaths.Values | Sort-Object)
}
function Write-ApiSnapshot
{
    $stagingDir = Join-Path $repoRoot '.docfx/api-snapshot'
    if (Test-Path $stagingDir)
    {
        Remove-Item $stagingDir -Recurse -Force
    }
    New-Item $stagingDir -ItemType Directory -Force | Out-Null

    $sanitizeRoots = @($repoRoot, $unityProjectRoot, $metadataProjectDir) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    try
    {
        foreach ($source in (Get-ChildItem $apiDir -File -Recurse))
        {
            $relative = $source.FullName.Substring($apiDir.Length).TrimStart([char]92, [char]47)
            $destination = Join-Path $stagingDir $relative
            $destinationDir = Split-Path $destination -Parent
            New-Item $destinationDir -ItemType Directory -Force | Out-Null

            if ($source.Extension -eq '.yml')
            {
                $text = [System.IO.File]::ReadAllText($source.FullName)
                foreach ($root in $sanitizeRoots)
                {
                    $trimmedRoot = $root.TrimEnd([char]92, [char]47)
                    $text = $text.Replace($trimmedRoot + [char]92, '')
                    $text = $text.Replace($trimmedRoot + [char]47, '')
                }                [System.IO.File]::WriteAllText(
                    $destination,
                    $text,
                    [System.Text.UTF8Encoding]::new($false))
            }
            else
            {
                Copy-Item $source.FullName $destination -Force
            }
        }

        if (Test-Path $snapshotPath)
        {
            Remove-Item $snapshotPath -Force
        }
        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $stagingDir,
            $snapshotPath,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $false)
    }
    finally
    {
        if (Test-Path $stagingDir)
        {
            Remove-Item $stagingDir -Recurse -Force
        }
    }

    $sourceHash = Get-ApiSourceHash
    [System.IO.File]::WriteAllText(
        $snapshotHashPath,
        $sourceHash + [Environment]::NewLine,
        [System.Text.UTF8Encoding]::new($false))
}
function Restore-ApiSnapshot
{
    if (-not (Test-Path $snapshotPath))
    {
        throw "API snapshot not found: $snapshotPath"
    }

    if (Test-Path $apiDir)
    {
        Remove-Item $apiDir -Recurse -Force
    }
    New-Item $apiDir -ItemType Directory -Force | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($snapshotPath, $apiDir)
}

function Assert-ApiSnapshotCurrent
{
    if (-not (Test-Path $snapshotHashPath))
    {
        throw "API snapshot hash not found: $snapshotHashPath"
    }

    $expected = ([System.IO.File]::ReadAllText($snapshotHashPath)).Trim()
    $actual = Get-ApiSourceHash
    if ($expected -ne $actual)
    {
        throw 'Generated API snapshot is stale. Run .\build-docs.ps1 in a Unity development checkout and commit DocsSite/generated-api.*.'
    }
}
