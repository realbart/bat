#!/usr/bin/env pwsh
# Build single-file executables for Windows, Linux, and macOS

$ErrorActionPreference = "Stop"
$outDir = "publish"

Write-Host "Building BAT single-file executables..." -ForegroundColor Cyan

# Clean previous builds
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Path $outDir | Out-Null

# Windows x64
Write-Host "`n[1/3] Building Windows (win-x64)..." -ForegroundColor Yellow
dotnet publish Bat/Bat.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$outDir/win-x64"

# Linux x64
Write-Host "`n[2/3] Building Linux (linux-x64)..." -ForegroundColor Yellow
dotnet publish Bat/Bat.csproj `
    -c Release `
    -r linux-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$outDir/linux-x64"

# macOS ARM64 (M1/M2/M3)
Write-Host "`n[3/3] Building macOS ARM64 (osx-arm64)..." -ForegroundColor Yellow
dotnet publish Bat/Bat.csproj `
    -c Release `
    -r osx-arm64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$outDir/osx-arm64"

Write-Host "`n✅ Build complete!" -ForegroundColor Green
Write-Host "Executables created in $outDir/:" -ForegroundColor Cyan
Get-ChildItem $outDir -Recurse -Include bat.exe,bat | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 1)
    Write-Host "  $($_.FullName.Replace($PWD, '.')) - ${size}MB" -ForegroundColor White
}

# Package Subst and XCopy as prefixed executables
Write-Host "`nPackaging prefixed executables..." -ForegroundColor Cyan
$prefix = [System.IO.File]::ReadAllBytes("prefix.bin")
foreach ($rid in "win-x64", "linux-x64", "osx-arm64") {
    foreach ($lib in "Subst", "Xcopy") {
        $dllPath = "$lib/bin/Release/net10.0/$lib.dll"
        $outPath = "$outDir/$rid/$($lib.ToLower()).exe"
        $dll = [System.IO.File]::ReadAllBytes($dllPath)
        [System.IO.File]::WriteAllBytes($outPath, [byte[]]($prefix + $dll))
        $size = [math]::Round(($prefix.Length + $dll.Length) / 1KB, 1)
        Write-Host "  ./$outPath - ${size}KB" -ForegroundColor White
    }
}

Write-Host "`nTo test:" -ForegroundColor Cyan
Write-Host "  Windows: .\$outDir\win-x64\bat.exe /?" -ForegroundColor White
Write-Host "  Linux:   chmod +x ./$outDir/linux-x64/bat && ./$outDir/linux-x64/bat -h" -ForegroundColor White
Write-Host "  macOS:   chmod +x ./$outDir/osx-arm64/bat && ./$outDir/osx-arm64/bat -h" -ForegroundColor White
