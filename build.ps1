# GameLoop & Windows Performance Optimizer - Build Script
[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [switch]$RunTests = $true,
    [switch]$PublishSingleFile = $true
)

$ErrorActionPreference = "Stop"

Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host " Building GameLoop & Windows Performance Optimizer ($Configuration)" -ForegroundColor Cyan
Write-Host "===================================================================" -ForegroundColor Cyan

# 1. Restore & Build Solution
Write-Host "`n[1/3] Restoring dependencies and compiling solution..." -ForegroundColor Yellow
dotnet build GameLoopOptimizer.sln -c $Configuration

# 2. Run Test Suite
if ($RunTests) {
    Write-Host "`n[2/3] Executing unit tests..." -ForegroundColor Yellow
    dotnet test tests/GameLoopOptimizer.Tests/GameLoopOptimizer.Tests.csproj -c $Configuration --no-build --verbosity normal
}

# 3. Publish Release
if ($PublishSingleFile) {
    Write-Host "`n[3/3] Publishing application bundle..." -ForegroundColor Yellow
    $publishDir = "d:/SourceCode/Gameloop_Opt/publish"
    
    # Terminate any running GameLoopOptimizer process before publish
    try {
        Get-Process -Name GameLoopOptimizer -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    } catch {}
    try {
        Get-CimInstance Win32_Process -Filter "Name = 'GameLoopOptimizer.exe'" -ErrorAction SilentlyContinue | Invoke-CimMethod -MethodName Terminate -ErrorAction SilentlyContinue | Out-Null
    } catch {}

    dotnet publish src/GameLoopOptimizer/GameLoopOptimizer.csproj -c $Configuration -r win-x64 --self-contained false -o $publishDir

    # Sync the .sav presets into publish\Assets.
    #
    # The publish step only copies what existed at build time; renaming a .sav (e.g. bumping
    # _v2 -> _v3) used to leave the OLD file in publish\Assets forever, and the app - whose
    # BundledSaveDirectory resolves to publish\Assets - served a stale preset with no warning.
    # Mirroring the .sav set makes the publish folder always agree with src\Assets.
    $srcAssets = "src/GameLoopOptimizer/Assets"
    $publishAssets = Join-Path $publishDir "Assets"
    if (Test-Path $srcAssets) {
        if (-not (Test-Path $publishAssets)) { New-Item -ItemType Directory -Path $publishAssets | Out-Null }
        Get-ChildItem $publishAssets -Filter *.sav -ErrorAction SilentlyContinue | Remove-Item -Force
        Get-ChildItem $srcAssets -Filter *.sav | Copy-Item -Destination $publishAssets -Force

        Write-Host "`nPreset save files in publish\Assets:" -ForegroundColor Yellow
        Get-ChildItem $publishAssets -Filter *.sav | ForEach-Object { Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1KB, 1)) KB)" }
    }

    Write-Host "`nBuild Completed Successfully!" -ForegroundColor Green
    Write-Host "Application executable location: $publishDir/GameLoopOptimizer.exe" -ForegroundColor Cyan
}
