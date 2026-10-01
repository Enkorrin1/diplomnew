param([string]$ProjectRoot = 'W:\BSUIR\DIPLOOM', [string]$BuildPath = 'Builds/FullGameDev/RogueDrive.exe', [int]$TestPort = 17780)
$ErrorActionPreference = 'Stop'
$run = Join-Path $ProjectRoot ('Temp/CoopAmmo/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$exe = Join-Path $ProjectRoot $BuildPath
if (!(Test-Path $exe)) { throw "Build not found: $exe" }
if (Get-NetUDPEndpoint -LocalPort $TestPort -ErrorAction SilentlyContinue) { throw "Port $TestPort is already in use." }
$processes = @()
$checks = [System.Collections.Generic.List[string]]::new()
$script:sequence = 0
function Send-Crew($role, $action) {
    $script:sequence++
    @{ sequence = $script:sequence; action = $action; brake = $true } | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $run "$role.command.json") -Encoding ascii
}
function Read-Crew($role) {
    try { Get-Content -LiteralPath (Join-Path $run "$role.snapshot.json") -Raw | ConvertFrom-Json } catch { $null }
}
function Wait-Crew($label, [scriptblock]$condition, $seconds = 45) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    do {
        if (& $condition) { $checks.Add("PASS $label"); Write-Output "PASS $label"; return }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "FAIL $label"
}
function Start-Crew($role) {
    $prefix = Join-Path $run $role
    $p = Start-Process -FilePath $exe -ArgumentList @('-screen-fullscreen','0','-screen-width','960','-screen-height','540','-coop-test-port',$TestPort,'-coop-probe',$prefix,'-logFile',"$prefix.log") -WindowStyle Hidden -PassThru
    $script:processes += $p
}
try {
    Start-Crew 'host'
    Wait-Crew 'host boot' { $null -ne (Read-Crew 'host') }
    Send-Crew 'host' 'host'
    Wait-Crew 'host session' { (Read-Crew 'host').connected }
    Start-Crew 'client'
    Wait-Crew 'client boot' { $null -ne (Read-Crew 'client') }
    Send-Crew 'client' 'join'
    Wait-Crew 'two connected in lobby' { (Read-Crew 'host').clientConnected -and (Read-Crew 'client').connected }
    Send-Crew 'host' 'ready'
    Send-Crew 'client' 'ready'
    Wait-Crew 'both reach bunker' { (Read-Crew 'host').scene -eq 'GarageScene' -and (Read-Crew 'client').scene -eq 'GarageScene' } 60
    Wait-Crew 'two players spawned' { (Read-Crew 'host').players -eq 2 -and (Read-Crew 'client').players -eq 2 }
    Wait-Crew 'initial magazines' { (Read-Crew 'host').ammo -eq 7 -and (Read-Crew 'client').ammo -eq 7 -and (Read-Crew 'client').reserveAmmo -eq 21 }
    for ($i = 6; $i -ge 0; $i--) {
        Send-Crew 'client' 'fire'
        Wait-Crew "client shot $($i + 1)" { (Read-Crew 'client').ammo -eq $i }
        Start-Sleep -Milliseconds 650
    }
    Send-Crew 'client' 'fire'
    Start-Sleep -Milliseconds 400
    if ((Read-Crew 'client').ammo -ne 0) { throw 'FAIL empty magazine accepted another shot' }
    $checks.Add('PASS empty magazine rejected')
    if ((Read-Crew 'host').ammo -ne 7) { throw 'FAIL host ammo changed by client shots' }
    $checks.Add('PASS host ammo independent')
    Send-Crew 'client' 'reload'
    Wait-Crew 'server begins reload' { (Read-Crew 'client').reloading }
    Wait-Crew 'server finishes reload and spends reserve' { (Read-Crew 'client').ammo -eq 7 -and (Read-Crew 'client').reserveAmmo -eq 14 -and !(Read-Crew 'client').reloading }
    $checks.Add('RESULT PASS')
} catch {
    $checks.Add($_.Exception.Message)
    Write-Output $_.Exception.Message
} finally {
    $checks | Set-Content -LiteralPath (Join-Path $run 'results.txt') -Encoding utf8
    foreach ($p in $processes) { if (!$p.HasExited) { Stop-Process -Id $p.Id } }
    Write-Output "REPORT $run"
}
if ($checks -notcontains 'RESULT PASS') { exit 1 }
