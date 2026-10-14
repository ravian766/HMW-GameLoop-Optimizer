param(
    [Parameter(Mandatory = $true)][string]$Label
)

$ErrorActionPreference = "Stop"
$adb = 'D:\Program Files\TxGameAssistant\ui\adb.exe'
$remote = '/sdcard/Android/data/com.tencent.ig/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/SaveGames/Active.sav'
$outDir = "$env:TEMP\asav_tiers"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$local = "$outDir\$Label.sav"
# adb writes its progress line to stderr, which PowerShell surfaces as an error record.
# Temporarily relax the preference so a successful pull is not treated as a failure.
# Target the VM explicitly: an emulator-XXXX serial also exists in the device list.
$prevEap = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
& $adb -s 127.0.0.1:5555 pull $remote $local 2>&1 | Out-Null
$ErrorActionPreference = $prevEap
if (-not (Test-Path $local)) { Write-Host "PULL FAILED for $Label" -ForegroundColor Red; exit 1 }

# Self-contained reader: finds "Name\0" + 12 + "IntProperty\0" + 4 + 4 + 1, then the int32.
# Mirrors ActiveSavService.FindIntPropertyOffset but with no dependency on the app assembly.
function Find-IntValue([byte[]]$buf, [string]$name) {
    $pat = [System.Text.Encoding]::ASCII.GetBytes($name + "`0")
    $hdr = [System.Text.Encoding]::ASCII.GetBytes("IntProperty`0")
    for ($i = 0; $i -le $buf.Length - $pat.Length - 25; $i++) {
        $ok = $true
        for ($j = 0; $j -lt $pat.Length; $j++) { if ($buf[$i + $j] -ne $pat[$j]) { $ok = $false; break } }
        if (-not $ok) { continue }
        if ($i -gt 0) {
            $prev = $buf[$i - 1]
            $isId = ($prev -ge 97 -and $prev -le 122) -or ($prev -ge 65 -and $prev -le 90) -or ($prev -ge 48 -and $prev -le 57) -or $prev -eq 95
            if ($isId) { continue }
        }
        $h = $i + $pat.Length
        $typeLen = [BitConverter]::ToInt32($buf, $h)
        if ($typeLen -ne 12) { continue }
        for ($k = 0; $k -lt 12; $k++) { if ($buf[$h + 4 + $k] -ne $hdr[$k]) { $typeLen = 0; break } }
        if ($typeLen -ne 12) { continue }
        $dataSize = [BitConverter]::ToInt32($buf, $h + 16)
        if ($dataSize -ne 4) { continue }
        return [BitConverter]::ToInt32($buf, $h + 25)
    }
    return $null
}

$b = [System.IO.File]::ReadAllBytes($local)
$fields = @(
    'BattleRenderQuality', 'LobbyRenderQuality', 'ManorRenderQuality',
    'MainCityRenderQuality', 'nEnhancedLobbyQuality',
    'GFBestQBattle', 'GFBestQLobby', 'GraphicFavor',
    'BattleRenderStyle', 'LobbyRenderStyle',
    'FPSLevel', 'BattleFPS', 'LobbyFPS', 'MainCityFPS'
)

Write-Host ""
Write-Host "=== $Label ===" -ForegroundColor Cyan
foreach ($f in $fields) {
    $v = Find-IntValue $b $f
    $disp = if ($null -eq $v) { "(absent)" } else { $v }
    Write-Host ("  {0,-24} = {1}" -f $f, $disp)
}
Write-Host ("  {0,-24} = {1} bytes" -f 'file size', $b.Length)
Write-Host "  saved -> $local" -ForegroundColor DarkGray