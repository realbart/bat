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
    $OutputPath = Join-Path $ProjectDirectory $OutputPath
}

Write-Host "Configuration: $Configuration"
Write-Host "OutputPath: $OutputPath"
Write-Host "ProjectDirectory: $ProjectDirectory"

$exes = @("Subst", "Doskey", "Xcopy")
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
        [string]$Extension
    )

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

    $dllPath = Join-Path $projectDir "bin\$Configuration\net10.0\$ProjectName.dll"

    if (-not (Test-Path $dllPath)) {
        Write-Error "DLL not found: $dllPath"
        exit 1
    }

    $dllBytes = [System.IO.File]::ReadAllBytes($dllPath)
    $outputFileName = $ProjectName.ToLower() + $Extension
    $outputFilePath = Join-Path $OutputPath $outputFileName

    $combinedBytes = [byte[]]($prefixBytes + $dllBytes)
    [System.IO.File]::WriteAllBytes($outputFilePath, $combinedBytes)

    Write-Host "Created: $outputFilePath"

    $pdbPath = Join-Path $projectDir "bin\$Configuration\net10.0\$ProjectName.pdb"
    if (Test-Path $pdbPath) {
        $pdbFileName = $ProjectName.ToLower() + ".pdb"
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

Write-Host "✅ AfterBuild completed successfully"