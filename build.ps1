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
    dotnet publish src/GameLoopOptimizer/GameLoopOptimizer.csproj -c $Configuration -r win-x64 --self-contained false -o $publishDir

    Write-Host "`nBuild Completed Successfully!" -ForegroundColor Green
    Write-Host "Application executable location: $publishDir/GameLoopOptimizer.exe" -ForegroundColor Cyan
}
