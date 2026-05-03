[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string[]]$RuntimeIdentifiers = @(),
    [string]$OutputRoot = (Join-Path $PSScriptRoot "publish")
)

$ErrorActionPreference = "Stop"

if ($RuntimeIdentifiers.Count -eq 0) {
    $onWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
    $onLinux   = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)
    $RuntimeIdentifiers = if ($onWindows)    { @("win-x64", "linux-x64") }
                          elseif ($onLinux)  { @("linux-x64") }
                          else               { @("osx-arm64") }
}

function Ensure-WslDotNet {
    # Check if dotnet is available in WSL
    $version = wsl -- bash -lc "dotnet --version 2>/dev/null" 2>$null
    if ($LASTEXITCODE -eq 0 -and $version -match '^10\.') {
        Write-Host "WSL dotnet $version found."
        return
    }

    Write-Host "Installing .NET 10 SDK in WSL..."
    wsl -- bash -lc "curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0"
    if ($LASTEXITCODE -ne 0) { throw "Failed to install .NET SDK in WSL." }

    # Persist PATH in .bashrc and .profile so future login shells find it
    wsl -- bash -lc @'
for f in ~/.bashrc ~/.profile; do
    grep -qxF 'export PATH=$PATH:$HOME/.dotnet' "$f" 2>/dev/null || echo 'export PATH=$PATH:$HOME/.dotnet' >> "$f"
done
'@
    Write-Host ".NET 10 SDK installed in WSL."
}

function Ensure-WslNativeAotPrereqs {
    # Check if clang or gcc is available (required for NativeAOT on Linux)
    $hasClang = wsl -- bash -lc "command -v clang 2>/dev/null" 2>$null
    if ($LASTEXITCODE -eq 0 -and $hasClang) { return }

    Write-Host "Installing NativeAOT prerequisites in WSL (clang, zlib)..."
    wsl -- bash -lc "apt-get update -qq && apt-get install -y clang zlib1g-dev"
    if ($LASTEXITCODE -ne 0) { throw "Failed to install NativeAOT prerequisites in WSL." }
    Write-Host "NativeAOT prerequisites installed."
}

$solutionDir = $PSScriptRoot
$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    [System.IO.Path]::GetFullPath($OutputRoot)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $solutionDir $OutputRoot))
}

$rootPublishTargets = @(
    @{ Name = "bat";  Project = "Bat\Bat.csproj";   Aot = $true  },
    @{ Name = "batd"; Project = "BatD\BatD.csproj"; Aot = $false }
)
$commandTargets = @(
    @{ Project = "Cmd"; Assembly = "Cmd"; Output = "cmd.exe" },
    @{ Project = "Doskey"; Assembly = "Doskey"; Output = "doskey.exe" },
    @{ Project = "Subst"; Assembly = "Subst"; Output = "subst.exe" },
    @{ Project = "Tree"; Assembly = "Tree"; Output = "tree.com" },
    @{ Project = "Xcopy"; Assembly = "Xcopy"; Output = "xcopy.exe" }
)

function Invoke-DotNet {
    param(
        [string[]]$Arguments,
        [string]$RuntimeIdentifier
    )

    $onLinux = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)
    if ($RuntimeIdentifier -and $RuntimeIdentifier.StartsWith("linux") -and -not $onLinux) {
        # Cross-OS AOT is not supported — route Linux builds through WSL
        Ensure-WslDotNet
        Ensure-WslNativeAotPrereqs
        $wslArgs = $Arguments | ForEach-Object {
            if ($_ -match '^([A-Za-z]):(.*)$') {
                '/mnt/' + $Matches[1].ToLower() + ($Matches[2] -replace '\\', '/')
            } else {
                $_ -replace '\\', '/'
            }
        }
        Write-Host "wsl dotnet $($wslArgs -join ' ')"
        wsl dotnet @wslArgs
    } else {
        Write-Host "dotnet $($Arguments -join ' ')"
        & dotnet @Arguments
    }

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

    # WSL routes linux builds through a Linux environment, so AOT is supported there too.
    $onWindows2 = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
    $onLinux2   = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)
    $isWslLinuxBuild = $RuntimeIdentifier.StartsWith("linux") -and $onWindows2
    $currentOs = if ($onWindows2) { "win" } elseif ($onLinux2) { "linux" } else { "osx" }
    $aotSupported = $Target.Aot -and ($RuntimeIdentifier.StartsWith($currentOs) -or $isWslLinuxBuild)

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
        "-o", $Destination,
        "--nologo"
    )

    if (-not $aotSupported) {
        $args += "/p:PublishAot=false"
    }

    Invoke-DotNet -Arguments $args -RuntimeIdentifier $RuntimeIdentifier
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
