# GameLoop Optimizer Release Verification Script
[CmdletBinding()]
param (
    [string]$PublishDir = "d:\SourceCode\Gameloop_Opt\publish"
)

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host " GameLoop Optimizer - Canonical Release Verifier" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

$exePath = Join-Path $PublishDir "GameLoopOptimizer.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "[FAIL] Executable not found at canonical location: $exePath" -ForegroundColor Red
    exit 1
}

Write-Host "`n[PASS] Found Executable: $exePath" -ForegroundColor Green

# 1. File Size & Timestamp
$fileItem = Get-Item $exePath
$sizeMb = [math]::Round($fileItem.Length / 1MB, 2)
Write-Host "  - Size: $sizeMb MB" -ForegroundColor White
Write-Host "  - Last Modified: $($fileItem.LastWriteTime)" -ForegroundColor White

# 2. File Version Information
$versionInfo = (Get-Item $exePath).VersionInfo
Write-Host "`n[+] Version Details:" -ForegroundColor Yellow
Write-Host "  - Product Name: $($versionInfo.ProductName)" -ForegroundColor White
Write-Host "  - Product Version: $($versionInfo.ProductVersion)" -ForegroundColor Cyan
Write-Host "  - File Version: $($versionInfo.FileVersion)" -ForegroundColor Cyan

# 3. Essential Dependencies Check
$requiredFiles = @(
    "GameLoopOptimizer.dll",
    "GameLoopOptimizer.runtimeconfig.json"
)

Write-Host "`n[+] Checking Bundle Dependencies in '$PublishDir'..." -ForegroundColor Yellow
$missing = 0
foreach ($req in $requiredFiles) {
    $full = Join-Path $PublishDir $req
    if (Test-Path $full) {
        Write-Host "  [OK] $req" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $req" -ForegroundColor Red
        $missing++
    }
}

if ($missing -gt 0) {
    Write-Host "`n[WARNING] $missing required dependency files were not found in publish directory." -ForegroundColor Red
    exit 1
}

Write-Host "`n======================================================" -ForegroundColor Cyan
Write-Host " Release Validation PASSED. Ready for distribution." -ForegroundColor Green
