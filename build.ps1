# QR-deFuzzer Build Script
# Produces:
#   dist/portable/QR-deFuzzer.exe  - Self-contained portable single-file EXE
#   dist/QR-deFuzzer-Setup.msi     - Windows Installer package

param(
    [switch]$SkipPortable,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$DistDir = Join-Path $ProjectDir "dist"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  QR-deFuzzer Build Script" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Clean dist
if (Test-Path $DistDir) {
    Write-Host "[*] Cleaning dist folder..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $DistDir
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

# ---- STEP 1: Portable Single-File EXE ----
if (-not $SkipPortable) {
    Write-Host ""
    Write-Host "[1/2] Building portable single-file EXE..." -ForegroundColor Green
    $portableDir = Join-Path $DistDir "portable"
    
    dotnet publish `
        -c Release `
        -r win-x64 `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        -o $portableDir
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[!] Portable build FAILED!" -ForegroundColor Red
        exit 1
    }

    # Rename to friendly name
    $exePath = Join-Path $portableDir "QR-deFuzzer.exe"
    if (Test-Path $exePath) {
        $finalExe = Join-Path $DistDir "QR-deFuzzer-Portable.exe"
        Copy-Item $exePath $finalExe -Force
        Write-Host "[+] Portable EXE: $finalExe" -ForegroundColor Green
    }
} else {
    Write-Host "[1/2] Skipping portable build." -ForegroundColor DarkGray
}

# ---- STEP 2: MSI Installer via WiX ----
if (-not $SkipInstaller) {
    Write-Host ""
    Write-Host "[2/2] Building MSI installer with WiX Toolset..." -ForegroundColor Green
    
    $publishDir = Join-Path $DistDir "portable"
    
    # Build the portable EXE first if it hasn't been built
    if (-not (Test-Path (Join-Path $publishDir "QR-deFuzzer.exe"))) {
        Write-Host "  -> Portable EXE not found, building first..." -ForegroundColor Yellow
        dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir
    }
    
    $msiOutput = Join-Path $DistDir "QR-deFuzzer-Setup.msi"
    
    wix build `
        -src (Join-Path $ProjectDir "installer.wxs") `
        -d PublishDir=$publishDir `
        -d ProjectDir=$ProjectDir `
        -o $msiOutput
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[!] MSI build FAILED!" -ForegroundColor Red
        Write-Host "    Make sure WiX Toolset v7 is installed: dotnet tool install --global wix" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "[+] MSI Installer: $msiOutput" -ForegroundColor Green
} else {
    Write-Host "[2/2] Skipping installer build." -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Build Complete!" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output files in: $DistDir" -ForegroundColor White
Get-ChildItem $DistDir -File | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  - $($_.Name)  ($sizeMB MB)" -ForegroundColor Gray
}
