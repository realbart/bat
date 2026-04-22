param(
    [Parameter(Mandatory=$true)]
    [string]$Configuration,

    [Parameter(Mandatory=$true)]
    [string]$OutputPath,

    [Parameter(Mandatory=$true)]
    [string]$ProjectDirectory
)

$ErrorActionPreference = "Stop"

if (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = [System.IO.Path]::GetFullPath((Join-Path $ProjectDirectory $OutputPath))
}

# Stop any running batd to release file locks before copying
taskkill /f /im batd.exe 2>$null
taskkill /f /im bat.exe 2>$null
Start-Sleep -Milliseconds 500

Write-Host "Configuration: $Configuration"
Write-Host "OutputPath: $OutputPath"
Write-Host "ProjectDirectory: $ProjectDirectory"

$exes = @("Subst", "Doskey", "Xcopy", "Cmd")
$coms = @("Tree")

$solutionDir = Split-Path $ProjectDirectory -Parent
$prefixBinPath = Join-Path $solutionDir "prefix.bin"

if (-not (Test-Path $prefixBinPath)) {
    Write-Error "prefix.bin not found at: $prefixBinPath"
    exit 1
}

$prefixBytes = [System.IO.File]::ReadAllBytes($prefixBinPath)

function ProcessFile {
    param(
        [string]$ProjectName,
        [string]$Extension,
        [string]$OutputName = ""
    )

    if ($OutputName -eq "") { $OutputName = $ProjectName }

    $projectDir = Join-Path $solutionDir $ProjectName
    $projectFile = Join-Path $projectDir "$ProjectName.csproj"

    if (Test-Path $projectFile) {
        Write-Host "Building $ProjectName..."
        dotnet build $projectFile -c $Configuration --nologo -v quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to build $ProjectName"
            exit $LASTEXITCODE
        }
    }

    $dllPath = Join-Path $projectDir "bin\$Configuration\net10.0\$OutputName.dll"

    if (-not (Test-Path $dllPath)) {
        Write-Error "DLL not found: $dllPath"
        exit 1
    }

    $dllBytes = [System.IO.File]::ReadAllBytes($dllPath)
    $outputFileName = $OutputName.ToLower() + $Extension
    $outputFilePath = Join-Path $OutputPath $outputFileName

    $combinedBytes = [byte[]]($prefixBytes + $dllBytes)
    [System.IO.File]::WriteAllBytes($outputFilePath, $combinedBytes)

    Write-Host "Created: $outputFilePath"

    $pdbPath = Join-Path $projectDir "bin\$Configuration\net10.0\$OutputName.pdb"
    if (Test-Path $pdbPath) {
        $pdbFileName = $OutputName.ToLower() + ".pdb"
        $pdbOutputPath = Join-Path $OutputPath $pdbFileName
        Copy-Item $pdbPath $pdbOutputPath -Force
        Write-Host "Copied: $pdbOutputPath"
    }
}

foreach ($exe in $exes) {
    ProcessFile -ProjectName $exe -Extension ".exe"
}

foreach ($com in $coms) {
    ProcessFile -ProjectName $com -Extension ".com"
}

# Copy prefixed satellites to BatD output dir so debugging BatD as startup project works
$batdOutputDir = Join-Path $solutionDir "BatD\bin\$Configuration\net10.0"
if (Test-Path $batdOutputDir) {
    foreach ($exe in $exes) {
        $src = Join-Path $OutputPath "$($exe.ToLower()).exe"
        if (Test-Path $src) { Copy-Item $src $batdOutputDir -Force; Write-Host "Copied to BatD: $($exe.ToLower()).exe" }
        $pdb = Join-Path $OutputPath "$($exe.ToLower()).pdb"
        if (Test-Path $pdb) { Copy-Item $pdb $batdOutputDir -Force }
    }
    foreach ($com in $coms) {
        $src = Join-Path $OutputPath "$($com.ToLower()).com"
        if (Test-Path $src) { Copy-Item $src $batdOutputDir -Force; Write-Host "Copied to BatD: $($com.ToLower()).com" }
        $pdb = Join-Path $OutputPath "$($com.ToLower()).pdb"
        if (Test-Path $pdb) { Copy-Item $pdb $batdOutputDir -Force }
    }
}

# Build and copy batd to bat's output directory
$batdProjectDir = Join-Path $solutionDir "BatD"
$batdProjectFile = Join-Path $batdProjectDir "BatD.csproj"
if (Test-Path $batdProjectFile) {
    Write-Host "Building BatD..."
    dotnet build $batdProjectFile -c $Configuration --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build BatD"
        exit $LASTEXITCODE
    }
    $batdBinDir = Join-Path $batdProjectDir "bin\$Configuration\net10.0"
    foreach ($file in @("batd.exe", "batd.dll", "batd.pdb", "batd.deps.json", "batd.runtimeconfig.json",
                        "Context.dll", "Context.pdb", "Cmd.dll", "Cmd.pdb")) {
        $src = Join-Path $batdBinDir $file
        if (Test-Path $src) {
            Copy-Item $src $OutputPath -Force
            Write-Host "Copied: $(Join-Path $OutputPath $file)"
        }
    }
}

Write-Host "AfterBuild completed successfully"