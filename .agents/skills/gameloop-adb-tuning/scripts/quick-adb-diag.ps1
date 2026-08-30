# GameLoop ADB Diagnostics & Status Inspection Script
[CmdletBinding()]
param (
    [string]$AdbPath = ""
)

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host " GameLoop ADB Engine Diagnostics & Property Inspector" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Resolve ADB Executable
if ([string]::IsNullOrWhiteSpace($AdbPath)) {
    $candidates = @(
        "D:\Program Files\TxGameAssistant\AppMarket\adb.exe",
        "C:\Program Files\TxGameAssistant\AppMarket\adb.exe",
        "D:\TxGameAssistant\AppMarket\adb.exe",
        "C:\TxGameAssistant\AppMarket\adb.exe",
        "adb.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) {
            $AdbPath = $c
            break
        }
    }
}

if (-not (Get-Command $AdbPath -ErrorAction SilentlyContinue) -and -not (Test-Path $AdbPath)) {
    Write-Host "[!] ADB executable could not be located. Please specify -AdbPath." -ForegroundColor Red
    exit 1
}

Write-Host "`n[+] Using ADB at: $AdbPath" -ForegroundColor Green

# 2. Check Device Connectivity
Write-Host "`n[+] Checking connected emulator instances..." -ForegroundColor Yellow
& $AdbPath devices

# 3. Inspect Key Performance Properties
$propsToInspect = @(
    "debug.sf.hw",
    "debug.composition.type",
    "debug.sf.latch_unsignaled",
    "debug.sf.enable_gl_backpressure",
    "debug.hwui.renderer",
    "persist.sys.display.rate",
    "dalvik.vm.heapgrowthlimit",
    "dalvik.vm.heapsize",
    "ro.product.model",
    "ro.product.manufacturer"
)

Write-Host "`n[+] Key Android VM In-Memory Properties:" -ForegroundColor Yellow
foreach ($prop in $propsToInspect) {
    $val = (& $AdbPath shell getprop $prop).Trim()
    Write-Host "  - $prop : " -NoNewline -ForegroundColor White
    if ([string]::IsNullOrWhiteSpace($val)) {
        Write-Host "(default / empty)" -ForegroundColor DarkGray
    } else {
        Write-Host "$val" -ForegroundColor Cyan
    }
}

Write-Host "`n======================================================" -ForegroundColor Cyan
Write-Host " Diagnostics Complete." -ForegroundColor Green
