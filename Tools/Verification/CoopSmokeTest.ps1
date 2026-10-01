param([string]$ProjectRoot = 'W:\BSUIR\DIPLOOM')
$ErrorActionPreference = 'Stop'
$run = Join-Path $ProjectRoot ('Temp/CoopTest/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$exe = Join-Path $ProjectRoot 'Builds/Coop/RogueDriveCoop.exe'
$processes = @()
$checks = [System.Collections.Generic.List[string]]::new()
$script:sequence = 0
function Send-CrewCommand($role, $action, $seat = -1, $throttle = 0, $steer = 0, $brake = $true) {
    if ($action -eq 'seat') { Start-Sleep -Milliseconds 250 }
    $script:sequence++
    $command = @{sequence=$script:sequence; action=$action; seat=$seat; throttle=$throttle; steer=$steer; brake=$brake; yaw=0} | ConvertTo-Json
    [System.IO.File]::WriteAllText((Join-Path $run "$role.command.json"), $command)
}
function Read-Crew($role) {
    try { return Get-Content -LiteralPath (Join-Path $run "$role.snapshot.json") -Raw | ConvertFrom-Json } catch { return $null }
}
function Wait-Crew($label, [scriptblock]$condition, $seconds = 20) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    do {
        if (& $condition) { $checks.Add("PASS $label"); Write-Output "PASS $label"; return }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "FAIL $label"
}
function Start-Crew($role) {
    $prefix = Join-Path $run $role
    $p = Start-Process -FilePath $exe -ArgumentList @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-coop-probe',$prefix,'-logFile',"$prefix.log") -WindowStyle Hidden -PassThru
    $script:processes += $p
}
try {
    Start-Crew 'host'
    Wait-Crew 'host application ready' { $null -ne (Read-Crew 'host') } 50
    Send-CrewCommand 'host' 'host'
    Wait-Crew 'host connected' { (Read-Crew 'host').connected }
    Start-Crew 'client'
    Wait-Crew 'client application ready' { $null -ne (Read-Crew 'client') } 50
    Send-CrewCommand 'client' 'join'
    Wait-Crew 'two players replicated' { (Read-Crew 'host').players -eq 2 -and (Read-Crew 'client').players -eq 2 }
    Wait-Crew 'shared vehicle replicated' { (Read-Crew 'client').driver -ne '' -and [Math]::Abs((Read-Crew 'host').vehiclePosition.z - (Read-Crew 'client').vehiclePosition.z) -lt .5 }
    Wait-Crew 'one camera and listener per player' { (Read-Crew 'host').cameraCount -eq 1 -and (Read-Crew 'client').cameraCount -eq 1 -and (Read-Crew 'host').listenerCount -eq 1 -and (Read-Crew 'client').listenerCount -eq 1 }
    Send-CrewCommand 'host' 'seat' 0
    Send-CrewCommand 'client' 'seat' 1
    Wait-Crew 'driver and navigator seated' { (Read-Crew 'host').seat -eq 0 -and (Read-Crew 'client').seat -eq 1 }
    Send-CrewCommand 'client' 'seat' 0
    Wait-Crew 'occupied driver seat rejected' { (Read-Crew 'client').seat -eq 1 -and (Read-Crew 'client').feedback -match 'занято' }
    $stationary = (Read-Crew 'host').vehiclePosition.z
    Send-CrewCommand 'client' 'input' 1 1 0 $false
    Start-Sleep -Seconds 2
    Wait-Crew 'passenger input cannot drive' { [Math]::Abs((Read-Crew 'host').vehiclePosition.z - $stationary) -lt .5 }
    Send-CrewCommand 'client' 'input'
    Start-Crew 'third'
    Wait-Crew 'third application ready' { $null -ne (Read-Crew 'third') } 50
    Send-CrewCommand 'third' 'join'
    Wait-Crew 'third player rejected' { (Read-Crew 'third').status -match 'заполнен' -and !(Read-Crew 'third').connected }
    Send-CrewCommand 'host' 'input' 0 1 0 $false
    Wait-Crew 'host drives shared car' { (Read-Crew 'host').speed -gt 3 -and (Read-Crew 'client').speed -gt 3 }
    Send-CrewCommand 'client' 'seat' -1
    Wait-Crew 'exit while moving rejected' { (Read-Crew 'client').seat -eq 1 -and (Read-Crew 'client').feedback -match 'остановите' }
    Send-CrewCommand 'host' 'input'
    Wait-Crew 'car stopped' { (Read-Crew 'host').speed -lt .3 }
    Send-CrewCommand 'host' 'seat' -1
    Wait-Crew 'host exits' { (Read-Crew 'host').seat -eq -1 }
    Send-CrewCommand 'client' 'seat' 0
    Wait-Crew 'client becomes driver' { (Read-Crew 'client').seat -eq 0 -and (Read-Crew 'host').driver -eq (Read-Crew 'client').localId }
    Send-CrewCommand 'host' 'seat' 1
    Wait-Crew 'host becomes navigator' { (Read-Crew 'host').seat -eq 1 }
    Send-CrewCommand 'host' 'capture' 1
    Send-CrewCommand 'client' 'capture' 0
    Start-Sleep -Seconds 1
    Send-CrewCommand 'client' 'input' 0 1 0 $false
    Wait-Crew 'remote driver moves authoritative car' { (Read-Crew 'host').speed -gt 3 -and (Read-Crew 'client').speed -gt 3 }
    Send-CrewCommand 'client' 'leave'
    Wait-Crew 'disconnected driver released and car brakes' { (Read-Crew 'host').driver -eq '18446744073709551615' -and (Read-Crew 'host').speed -lt .5 -and (Read-Crew 'host').players -eq 1 }
    Send-CrewCommand 'host' 'leave'
    Wait-Crew 'host shutdown clears scene actors' { !(Read-Crew 'host').connected -and (Read-Crew 'host').players -eq 0 }
    Send-CrewCommand 'host' 'host'
    Wait-Crew 'host can start a new session' { (Read-Crew 'host').connected -and (Read-Crew 'host').players -eq 1 }
    Send-CrewCommand 'client' 'join'
    Wait-Crew 'client can rejoin a fresh session' { (Read-Crew 'client').connected -and (Read-Crew 'host').players -eq 2 }
    Send-CrewCommand 'client' 'menu'
    Start-Sleep -Milliseconds 400
    Send-CrewCommand 'client' 'capture'
    Start-Sleep -Seconds 1
    $checks.Add('RESULT PASS')
} catch {
    $checks.Add($_.Exception.Message)
    Write-Output $_.Exception.Message
} finally {
    $checks | Set-Content -LiteralPath (Join-Path $run 'results.txt') -Encoding utf8
    [System.IO.File]::WriteAllText((Join-Path $ProjectRoot 'Artifacts/Coop/latest-test-path.txt'), $run)
    foreach ($p in $processes) { if (!$p.HasExited) { Stop-Process -Id $p.Id } }
    Write-Output "REPORT $run"
}
if ($checks -notcontains 'RESULT PASS') { exit 1 }
