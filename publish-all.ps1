[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string[]]$RuntimeIdentifiers = @("win-x64", "linux-x64", "osx-arm64"),
    [string]$OutputRoot = (Join-Path $PSScriptRoot "publish")
)

$ErrorActionPreference = "Stop"

$solutionDir = $PSScriptRoot
$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $solutionDir $OutputRoot))
}

$rootPublishTargets = @(
    @{ Name = "bat"; Project = "Bat\Bat.csproj" },
    @{ Name = "batd"; Project = "BatD\BatD.csproj" }
)
$commandTargets = @(
    @{ Project = "Cmd"; Assembly = "Cmd"; Output = "cmd.exe" },
    @{ Project = "Doskey"; Assembly = "Doskey"; Output = "doskey.exe" },
    @{ Project = "Subst"; Assembly = "Subst"; Output = "subst.exe" },
    @{ Project = "Tree"; Assembly = "Tree"; Output = "tree.com" },
    @{ Project = "Xcopy"; Assembly = "Xcopy"; Output = "xcopy.exe" }
)

function Invoke-DotNet {
    param([string[]]$Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE."
    }
}

function Publish-RootTarget {
    param(
        [hashtable]$Target,
        [string]$RuntimeIdentifier,
        [string]$Destination
    )

    $args = @(
        "publish",
        (Join-Path $solutionDir $Target.Project),
        "-c", $Configuration,
        "-r", $RuntimeIdentifier,
        "--self-contained", "true",
        "/p:PublishSingleFile=true",
        "/p:DebugType=None",
        "/p:DebugSymbols=false",
        "/p:IncludeSymbolsInSingleFile=false",
        "/p:PublishAot=false",
        "-o", $Destination,
        "--nologo"
    )

    Invoke-DotNet -Arguments $args
}

function Build-CommandLibrary {
    param([string]$ProjectName)

    $projectPath = Join-Path $solutionDir "$ProjectName\$ProjectName.csproj"
    $args = @(
        "build",
        $projectPath,
        "-c", $Configuration,
        "/p:UseAppHost=false",
        "/p:PublishAot=false",
        "/p:DebugType=None",
        "/p:DebugSymbols=false",
        "--nologo"
    )

    Invoke-DotNet -Arguments $args
}

function Get-BuildDllPath {
    param(
        [string]$ProjectName,
        [string]$AssemblyName
    )

    $dllPath = Join-Path (Join-Path $solutionDir $ProjectName) "bin\$Configuration\net10.0\$AssemblyName.dll"
    if (-not (Test-Path $dllPath)) {
        throw "Could not find $AssemblyName.dll for $ProjectName."
    }

    return $dllPath
}

function New-CommandWrapper {
    param(
        [string]$DllPath,
        [string]$OutputPath,
        [byte[]]$PrefixBytes
    )

    $dllBytes = [System.IO.File]::ReadAllBytes($DllPath)
    $combinedBytes = [byte[]]($PrefixBytes + $dllBytes)
    [System.IO.File]::WriteAllBytes($OutputPath, $combinedBytes)
}

function Publish-CommandWrappers {
    param(
        [string]$Destination,
        [byte[]]$PrefixBytes
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    foreach ($commandTarget in $commandTargets) {
        Build-CommandLibrary -ProjectName $commandTarget.Project
        $dllPath = Get-BuildDllPath -ProjectName $commandTarget.Project -AssemblyName $commandTarget.Assembly
        $outputPath = Join-Path $Destination $commandTarget.Output
        New-CommandWrapper -DllPath $dllPath -OutputPath $outputPath -PrefixBytes $PrefixBytes
        Write-Host "Created: $outputPath"
    }
}

$prefixPath = Join-Path $solutionDir "prefix.bin"
if (-not (Test-Path $prefixPath)) {
    throw "prefix.bin not found at $prefixPath"
}
$prefixBytes = [System.IO.File]::ReadAllBytes($prefixPath)

if (Test-Path $outputRootPath) {
    Remove-Item $outputRootPath -Recurse -Force
}
New-Item -ItemType Directory -Path $outputRootPath -Force | Out-Null

# Build command wrappers once — they are identical for all distributions.
$sharedBinPath = Join-Path $outputRootPath "bin"
Publish-CommandWrappers -Destination $sharedBinPath -PrefixBytes $prefixBytes

foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    $destination = Join-Path $outputRootPath $runtimeIdentifier
    $commandDestination = Join-Path $destination "bin"

    New-Item -ItemType Directory -Path $destination -Force | Out-Null

    foreach ($target in $rootPublishTargets) {
        Publish-RootTarget -Target $target -RuntimeIdentifier $runtimeIdentifier -Destination $destination
    }

    # Copy the shared bin into each runtime folder.
    Copy-Item -Path $sharedBinPath -Destination $destination -Recurse -Force
}

Remove-Item $sharedBinPath -Recurse -Force

Write-Host "Publish completed: $outputRootPath"
