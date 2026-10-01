param([string]$ProjectRoot = 'W:\BSUIR\DIPLOOM', [string]$BuildPath = 'Builds/CoopAudit/RogueDriveCoop.exe', [int]$TestPort = 17778)
$ErrorActionPreference = 'Stop'
$run = Join-Path $ProjectRoot ('Temp/CoopLifecycle/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$exe = Join-Path $ProjectRoot $BuildPath
if (Get-NetUDPEndpoint -LocalPort $TestPort -ErrorAction SilentlyContinue) { throw "Test port $TestPort is already in use." }
$processes = @()
$checks = [System.Collections.Generic.List[string]]::new()
$script:sequence = 0
function Send-Crew($role, $action) {
    $script:sequence++
    $command = @{sequence=$script:sequence; action=$action; brake=$true} | ConvertTo-Json
    [System.IO.File]::WriteAllText((Join-Path $run "$role.command.json"), $command)
}
function Read-Crew($role) {
    try { Get-Content -LiteralPath (Join-Path $run "$role.snapshot.json") -Raw | ConvertFrom-Json } catch { $null }
}
function Wait-Crew($label, [scriptblock]$condition, $seconds = 45) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    do {
        if (& $condition) { $checks.Add("PASS $label"); Write-Output "PASS $label"; return }
        Start-Sleep -Milliseconds 150
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
    Wait-Crew 'host application ready' { $null -ne (Read-Crew 'host') }
    Send-Crew 'host' 'host'
    Wait-Crew 'host lobby connected' { (Read-Crew 'host').connected }
    Send-Crew 'host' 'ready'
    Wait-Crew 'host alone cannot launch' { (Read-Crew 'host').hostReady -and (Read-Crew 'host').countdown -lt 0 }
    Start-Crew 'client'
    Wait-Crew 'client application ready' { $null -ne (Read-Crew 'client') }
    Send-Crew 'client' 'join'
    Wait-Crew 'both peers see connected crew' { (Read-Crew 'host').clientConnected -and (Read-Crew 'client').clientConnected }
    Wait-Crew 'host readiness replicated' { (Read-Crew 'client').hostReady }
    Send-Crew 'client' 'ready'
    Wait-Crew 'countdown synchronized' { (Read-Crew 'host').countdown -gt 0 -and (Read-Crew 'client').countdown -gt 0 }
    Send-Crew 'client' 'ready'
    Wait-Crew 'unready cancels countdown' { (Read-Crew 'host').countdown -lt 0 -and !(Read-Crew 'client').clientReady }
    Start-Crew 'third'
    Wait-Crew 'third application ready' { $null -ne (Read-Crew 'third') }
    Send-Crew 'third' 'join'
    Wait-Crew 'full crew rejects third peer' { (Read-Crew 'third').status -match 'заполнен' -and !(Read-Crew 'third').connected }
    Send-Crew 'client' 'ready'
    Wait-Crew 'both peers enter bunker with two players' { (Read-Crew 'host').scene -eq 'GarageScene' -and (Read-Crew 'client').scene -eq 'GarageScene' -and (Read-Crew 'host').players -eq 2 -and (Read-Crew 'client').players -eq 2 } 90
    Wait-Crew 'one camera and audio listener on each peer' { (Read-Crew 'host').cameraCount -eq 1 -and (Read-Crew 'client').cameraCount -eq 1 -and (Read-Crew 'host').listenerCount -eq 1 -and (Read-Crew 'client').listenerCount -eq 1 }
    Send-Crew 'host' 'capture'
    Send-Crew 'client' 'capture'
    Start-Sleep -Seconds 2
    Send-Crew 'client' 'leave'
    Wait-Crew 'client returns to menu after shutdown' { !(Read-Crew 'client').connected -and (Read-Crew 'client').scene -eq 'MainMenuScene' }
    Wait-Crew 'host removes disconnected player' { (Read-Crew 'host').players -eq 1 }
    Send-Crew 'client' 'join'
    Wait-Crew 'client rejoins bunker' { (Read-Crew 'client').connected -and (Read-Crew 'client').players -eq 2 }
    Send-Crew 'host' 'leave'
    Wait-Crew 'host departure returns both to menu' { !(Read-Crew 'host').connected -and !(Read-Crew 'client').connected -and (Read-Crew 'host').scene -eq 'MainMenuScene' -and (Read-Crew 'client').scene -eq 'MainMenuScene' }
    Send-Crew 'host' 'host'
    Wait-Crew 'same process can host again' { (Read-Crew 'host').connected }
    Send-Crew 'client' 'join'
    Wait-Crew 'same client can join again' { (Read-Crew 'client').connected -and (Read-Crew 'client').clientConnected }
    Wait-Crew 'new session clears readiness' { !(Read-Crew 'host').hostReady -and !(Read-Crew 'client').clientReady -and (Read-Crew 'host').countdown -lt 0 }
    $runtimeErrors = Select-String -Path (Join-Path $run '*.log') -Pattern 'Exception:|GameObject has multiple AudioSources|\[Netcode\].*(Error|Failed)'
    if ($runtimeErrors) { throw ('FAIL runtime errors: ' + ($runtimeErrors | Select-Object -First 1).Line) }
    $checks.Add('PASS no runtime exceptions or audio filter conflicts')
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
