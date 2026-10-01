param([string]$ProjectRoot = 'W:\BSUIR\DIPLOOM', [string]$BuildPath = 'Builds/CoopAudit/RogueDriveCoop.exe', [switch]$HostDrives, [switch]$SupplyChecks, [switch]$CombatChecks,
    [ValidateSet('None','Client','Host')][string]$DisconnectOnTransition = 'None', [int]$TestPort = 17779)
$ErrorActionPreference = 'Stop'
$run = Join-Path $ProjectRoot ('Temp/CoopDeparture/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$exe = Join-Path $ProjectRoot $BuildPath
$processes = @()
$checks = [System.Collections.Generic.List[string]]::new()
$script:sequence = 0
$driverRole = if ($HostDrives) { 'host' } else { 'client' }
$hostSeat = if ($HostDrives) { 0 } else { 1 }
$clientSeat = 1 - $hostSeat
function Send-Crew($role, $action, $seat=0, $station=0, $throttle=0, $yaw=0, $steer=0) {
    $script:sequence++
    $command = @{sequence=$script:sequence; action=$action; seat=$seat; station=$station; throttle=$throttle; yaw=$yaw; steer=$steer; brake=($throttle -eq 0)} | ConvertTo-Json
    $commandPath = Join-Path $run "$role.command.json"
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try { [System.IO.File]::WriteAllText($commandPath, $command); break }
        catch [System.IO.IOException] {
            if ($attempt -eq 39) { throw }
            Start-Sleep -Milliseconds 25
        }
    }
    $roleIndex = if ($role -eq 'host') { 0 } else { 1 }
    if ($processes.Count -gt $roleIndex -and $processes[$roleIndex].HasExited) { return }
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $snapshot = Read-Crew $role
        if ($null -ne $snapshot -and $snapshot.commandSequence -ge $script:sequence) { return }
        Start-Sleep -Milliseconds 25
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Probe did not acknowledge $role command $action (sequence $script:sequence)"
}
function Read-Crew($role) {
    try { Get-Content -LiteralPath (Join-Path $run "$role.snapshot.json") -Raw | ConvertFrom-Json } catch { $null }
}
function Read-Cache($role) {
    $snapshot = Read-Crew $role
    if ([string]::IsNullOrEmpty($snapshot.cacheState)) { return $null }
    try { $snapshot.cacheState | ConvertFrom-Json } catch { $null }
}
function Wait-Crew($label, [scriptblock]$condition, $seconds = 35) {
    $deadline = [DateTime]::UtcNow.AddSeconds($seconds)
    do {
        if (& $condition) { $checks.Add("PASS $label"); Write-Output "PASS $label"; return }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "FAIL $label"
}
function Start-Crew($role) {
    $prefix = Join-Path $run $role
    $p = Start-Process -FilePath $exe -ArgumentList @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-coop-probe',$prefix,'-coop-test-port',"$TestPort",'-logFile',"$prefix.log") -WindowStyle Hidden -PassThru
    $script:processes += $p
}
try {
    Start-Crew 'host'
    Wait-Crew 'host application ready' { $null -ne (Read-Crew 'host') }
    Send-Crew 'host' 'host'
    Wait-Crew 'host connected' { (Read-Crew 'host').connected }
    Start-Crew 'client'
    Wait-Crew 'client application ready' { $null -ne (Read-Crew 'client') }
    Send-Crew 'client' 'join'
    Wait-Crew 'client connected' { (Read-Crew 'client').connected }
    Send-Crew 'host' 'ready'
    Send-Crew 'client' 'ready'
    Wait-Crew 'shared quest spawned on both peers' { (Read-Crew 'host').questSpawned -and (Read-Crew 'client').questSpawned -and (Read-Crew 'client').players -eq 2 } 90
    Wait-Crew 'one crew canvas per peer' { (Read-Crew 'host').crewCanvases -eq 1 -and (Read-Crew 'client').crewCanvases -eq 1 }
    Send-Crew 'client' 'station' 0 0
    Start-Sleep -Seconds 1
    Wait-Crew 'remote generator request rejected at distance' { !(Read-Crew 'host').generator }
    $fields = @('generator','wheel','battery','fuel','keys','gates')
    foreach ($station in 0..5) {
        $field = $fields[$station]
        foreach ($attempt in 0..15) {
            Send-Crew 'host' 'place_station' 1 $station 0 $attempt
            Start-Sleep -Milliseconds 650
            Send-Crew 'client' 'station' 0 $station
            Start-Sleep -Milliseconds 450
            if ((Read-Crew 'host').$field -and (Read-Crew 'client').$field) { break }
        }
        Wait-Crew "station $field replicated" { (Read-Crew 'host').$field -and (Read-Crew 'client').$field }
        if ($station -eq 3) {
            Start-Sleep -Milliseconds 300
            Send-Crew 'client' 'station' 0 3
            Wait-Crew 'fuel capped at 15 liters on both peers' { (Read-Crew 'host').fuel -eq 15 -and (Read-Crew 'client').fuel -eq 15 }
        }
    }
    Send-Crew 'client' 'leave'
    Wait-Crew 'client leaves prepared bunker' { !(Read-Crew 'client').connected -and (Read-Crew 'client').scene -eq 'MainMenuScene' }
    Send-Crew 'client' 'join'
    Wait-Crew 'late join restores preparation' { (Read-Crew 'client').questSpawned -and (Read-Crew 'client').gates -and (Read-Crew 'client').fuel -eq 15 }
    Send-Crew 'host' 'place_car' 0
    Wait-Crew 'host approaches car' { $s = Read-Crew 'host'; [Math]::Abs($s.playerPosition.z - $s.vehiclePosition.z) -lt 1 }
    Send-Crew 'host' 'seat' 0
    Wait-Crew 'host tests departure without passenger' { (Read-Crew 'host').seat -eq 0 }
    $beforeSolo = (Read-Crew 'host').vehiclePosition.z
    Send-Crew 'host' 'input' 0 0 1
    Start-Sleep -Seconds 2
    Wait-Crew 'departure blocked while teammate is on foot' { [Math]::Abs((Read-Crew 'host').vehiclePosition.z - $beforeSolo) -lt .5 }
    Send-Crew 'host' 'seat' $hostSeat
    Wait-Crew 'host selected role seated' { if ((Read-Crew 'host').seat -ne $hostSeat) { Send-Crew 'host' 'seat' $hostSeat }; (Read-Crew 'host').seat -eq $hostSeat }
    Send-Crew 'host' 'place_car' 1
    Wait-Crew 'client approaches car' { $s = Read-Crew 'client'; [Math]::Abs($s.playerPosition.z - $s.vehiclePosition.z) -lt 1 }
    Send-Crew 'client' 'seat' $clientSeat
    Wait-Crew 'client selected role seated' { if ((Read-Crew 'client').seat -ne $clientSeat) { Send-Crew 'client' 'seat' $clientSeat }; (Read-Crew 'client').seat -eq $clientSeat }
    Send-Crew 'host' 'damage'
    Wait-Crew 'host health set before departure' { (Read-Crew 'host').health -eq 85 }
    Send-Crew 'host' 'capture'
    Send-Crew 'client' 'capture'
    Wait-Crew 'bunker screenshots saved' { (Test-Path (Join-Path $run 'client.png')) -and (Test-Path (Join-Path $run 'host.png')) } 20
    Copy-Item (Join-Path $run 'client.png') (Join-Path $run 'bunker.png')
    $vehicleId = (Read-Crew 'host').vehicleId
    $hostPlayerId = (Read-Crew 'host').playerId
    $clientPlayerId = (Read-Crew 'client').playerId
    Send-Crew $driverRole 'input' 0 0 .8
    $script:interrupted = $false
    $arrivalLabel = if ($DisconnectOnTransition -eq 'None') { 'both peers arrive on road' } else { 'transition observed and process interrupted' }
    Wait-Crew $arrivalLabel {
        $s = Read-Crew 'host'
        if ($DisconnectOnTransition -ne 'None' -and $s.transitioning -and !$script:interrupted) {
            $index = if ($DisconnectOnTransition -eq 'Host') { 0 } else { 1 }
            Stop-Process -Id $processes[$index].Id
            $script:interrupted = $true
        }
        if ($script:interrupted) { return $true }
        if ($s.scene -eq 'GarageScene' -and !$s.transitioning) {
            $steering = [Math]::Clamp((-$s.vehiclePosition.x * 5 - $s.vehicleYaw) * .035, -.5, .5)
            Send-Crew $driverRole 'input' 0 0 .8 0 $steering
        }
        $s.scene -in @('Stage1_Outskirts','JourneySession') -and
            (Read-Crew 'client').scene -eq $s.scene -and (Read-Crew 'client').departed
    } 90
    if ($DisconnectOnTransition -ne 'None') {
        if (!$script:interrupted) { throw 'FAIL transition was not observed before disconnect' }
        if ($DisconnectOnTransition -eq 'Host') {
            Wait-Crew 'host loss during load returns client to menu' { !(Read-Crew 'client').connected -and (Read-Crew 'client').scene -eq 'MainMenuScene' } 90
        } else {
            Wait-Crew 'client loss during load releases slot and completes host load' { $s=Read-Crew 'host'; $s.scene -in @('Stage1_Outskirts','JourneySession') -and !$s.transitioning -and $s.players -eq 1 -and $s.driver -eq '18446744073709551615' } 90
            Wait-Crew 'car stops after driver loss during load' { (Read-Crew 'host').speed -lt .5 }
        }
    } else {
    Send-Crew $driverRole 'input'
    Wait-Crew 'same vehicle and players survive transition' { (Read-Crew 'host').vehicleId -eq $vehicleId -and (Read-Crew 'host').playerId -eq $hostPlayerId -and (Read-Crew 'client').playerId -eq $clientPlayerId }
    Wait-Crew 'roles fuel and health preserved' { (Read-Crew 'host').seat -eq $hostSeat -and (Read-Crew 'client').seat -eq $clientSeat -and (Read-Crew 'host').health -eq 85 -and (Read-Crew 'client').fuel -eq 15 }
    Wait-Crew 'one vehicle camera listener and HUD on both peers' { (Read-Crew 'host').vehicles -eq 1 -and (Read-Crew 'client').vehicles -eq 1 -and (Read-Crew 'host').cameraCount -eq 1 -and (Read-Crew 'client').cameraCount -eq 1 -and (Read-Crew 'host').listenerCount -eq 1 -and (Read-Crew 'client').listenerCount -eq 1 -and (Read-Crew 'client').crewCanvases -eq 1 }
    if ($SupplyChecks) {
        Wait-Crew 'shared supplies initialized on both peers' { (Read-Crew 'host').suppliesSpawned -and (Read-Crew 'client').suppliesSpawned -and (Read-Crew 'host').sharedAmmo -eq 0 -and (Read-Crew 'client').sharedAmmo -eq 0 }
        Wait-Crew 'storm radar synchronized for both crew roles' { [Math]::Abs((Read-Crew 'host').stormDistance - (Read-Crew 'client').stormDistance) -lt 10 }
        Send-Crew 'client' 'cache' 0 0
        Start-Sleep -Seconds 1
        Wait-Crew 'distant and seated cache request grants nothing' { (Read-Crew 'host').sharedAmmo -eq 0 -and (Read-Crew 'host').medkits -eq 0 }
        Send-Crew 'host' 'place_cache' 1 0
        Wait-Crew 'remote player approaches actual roadside cache' { $s=Read-Crew 'client'; $s.seat -eq -1 -and $s.activeCaches -ge 1 }
        Send-Crew 'client' 'cache' 0 0
        Wait-Crew 'timed search synchronized to both peers' { (Read-Crew 'host').searchProgress -gt .01 -and (Read-Crew 'client').searchProgress -gt .01 }
        Send-Crew 'host' 'place_cache' 0 0
        Start-Sleep -Milliseconds 700
        Send-Crew 'host' 'cache' 0 0
        Wait-Crew 'competing actor cannot steal an ongoing search' { (Read-Crew 'host').feedback -like '*напарник*' -and (Read-Crew 'host').sharedAmmo -eq 0 }
        Send-Crew 'client' 'cache' 0 0
        Wait-Crew 'owner cancels search on both peers' { (Read-Crew 'host').searchProgress -eq 0 -and (Read-Crew 'client').searchProgress -eq 0 }
        Send-Crew 'client' 'cache' 0 0
        Wait-Crew 'cancelled search starts again' { (Read-Crew 'host').searchProgress -gt .01 }
        Send-Crew 'host' 'place_cache' 1 0 8
        Wait-Crew 'leaving interaction radius cancels without rewards' { (Read-Crew 'host').searchProgress -eq 0 -and (Read-Crew 'host').sharedAmmo -eq 0 }
        Send-Crew 'host' 'place_cache' 1 0
        Start-Sleep -Milliseconds 700
        Send-Crew 'client' 'cache' 0 0
        Wait-Crew 'search restarts after walking back' { (Read-Crew 'host').searchProgress -gt .01 }
        Send-Crew 'host' 'fixture' 1 4
        Wait-Crew 'downed actor cannot continue search' { (Read-Crew 'client').downed -and (Read-Crew 'host').searchProgress -eq 0 -and (Read-Crew 'host').sharedAmmo -eq 0 }
        Send-Crew 'host' 'fixture' 1 3
        Wait-Crew 'downed fixture recovered before next search' { !(Read-Crew 'client').downed -and (Read-Crew 'client').health -eq 40 }
        Send-Crew 'host' 'fixture' 1 0
        $stormBeforeSearch = (Read-Crew 'host').stormFront
        Start-Sleep -Milliseconds 700
        Send-Crew 'client' 'cache' 0 0
        Wait-Crew 'full shared inventory preserves opened cache rewards' { $c=Read-Cache 'client'; $null -ne $c -and $c.opened -and $c.rewards[0].remaining -eq 28 -and (Read-Crew 'host').sharedAmmo -eq 168 } 25
        Wait-Crew 'storm continues advancing during shared cache search' { (Read-Crew 'host').stormFront -gt $stormBeforeSearch }
        Send-Crew 'host' 'fixture' 1 1
        Start-Sleep -Milliseconds 700
        Send-Crew 'host' 'cache' 0 0
        Send-Crew 'client' 'cache' 0 0
        Wait-Crew 'simultaneous claim grants patrol cache exactly once' { (Read-Crew 'host').sharedAmmo -eq 28 -and (Read-Crew 'client').sharedAmmo -eq 28 -and (Read-Crew 'host').medkits -eq 1 -and (Read-Crew 'host').bolts -eq 4 }
        Send-Crew 'client' 'cache' 0 0
        Start-Sleep -Seconds 1
        Wait-Crew 'repeated empty cache request grants nothing' { (Read-Crew 'host').sharedAmmo -eq 28 -and (Read-Crew 'client').sharedAmmo -eq 28 -and (Read-Crew 'client').stop1 }
        Send-Crew 'host' 'place_cache' 1 1
        Send-Crew 'host' 'inspect' 0 1
        Start-Sleep -Milliseconds 700
        Send-Crew 'client' 'cache' 0 1
        Wait-Crew 'medical cache supplies shared by both peers' { (Read-Crew 'host').sharedAmmo -eq 42 -and (Read-Crew 'client').sharedAmmo -eq 42 -and (Read-Crew 'host').medkits -eq 3 -and (Read-Crew 'client').batteries -eq 1 -and (Read-Crew 'client').stop2 } 20
        Send-Crew 'host' 'place_car' 0
        Send-Crew 'host' 'seat' 1
        Wait-Crew 'navigator boards to mark the next supply stop' { (Read-Crew 'host').seat -eq 1 }
        Send-Crew 'host' 'crew_action' 0 0
        Wait-Crew 'navigator marker synchronized to driver' { (Read-Crew 'host').marker -eq 'route0/supply/2' -and (Read-Crew 'client').marker -eq 'route0/supply/2' }
        Send-Crew 'host' 'place_cache' 1 2
        Send-Crew 'host' 'inspect' 0 2
        Start-Sleep -Milliseconds 700
        Send-Crew 'client' 'cache' 0 2
        Wait-Crew 'repair cache search starts before connection loss' { (Read-Crew 'host').searchProgress -gt .01 }
        Send-Crew 'client' 'leave'
        Wait-Crew 'disconnect releases cache search lock' { !(Read-Crew 'client').connected -and (Read-Crew 'host').searchProgress -eq 0 -and (Read-Crew 'host').sharedAmmo -eq 42 }
        Send-Crew 'client' 'join'
        Wait-Crew 'road reconnect restores shared supplies and marker' { (Read-Crew 'client').suppliesSpawned -and (Read-Crew 'client').sharedAmmo -eq 42 -and (Read-Crew 'client').medkits -eq 3 -and (Read-Crew 'client').marker -eq 'route0/supply/2' } 90
        Wait-Crew 'road reconnect restores visible shared HUD on both peers' { (Read-Crew 'host').crewCanvases -eq 1 -and (Read-Crew 'client').crewCanvases -eq 1 -and (Read-Crew 'host').supplyHudVisible -and (Read-Crew 'client').supplyHudVisible }
        Send-Crew 'client' 'inspect' 0 0
        Send-Crew 'host' 'inspect' 0 0
        Wait-Crew 'late join retains exhausted patrol cache history' { $c=Read-Cache 'client'; $null -ne $c -and $c.opened -and ($c.rewards | Where-Object remaining -gt 0).Count -eq 0 -and (Read-Crew 'client').cacheState -eq (Read-Crew 'host').cacheState }
        Send-Crew 'host' 'place_cache' 1 2
        Send-Crew 'host' 'inspect' 0 2
        Start-Sleep -Milliseconds 700
        Send-Crew 'client' 'cache' 0 2
        Wait-Crew 'repair cache completes after reconnect without duplication' { (Read-Crew 'host').sharedAmmo -eq 49 -and (Read-Crew 'client').sharedAmmo -eq 49 -and (Read-Crew 'host').bolts -eq 12 -and (Read-Crew 'client').wrench -and (Read-Crew 'client').stop3 } 25
        Send-Crew 'host' 'fixture' 1 2
        Start-Sleep -Milliseconds 500
        Send-Crew 'client' 'reload'
        Wait-Crew 'remote pistol reload spends shared ammunition' { (Read-Crew 'client').ammo -eq 7 -and (Read-Crew 'host').sharedAmmo -eq 42 -and (Read-Crew 'client').sharedAmmo -eq 42 }
        Send-Crew 'host' 'place_cache' 0 2
        Send-Crew 'host' 'fixture' 0 2
        Send-Crew 'host' 'reload'
        Wait-Crew 'host pistol reload uses the same shared pool' { (Read-Crew 'host').ammo -eq 7 -and (Read-Crew 'host').sharedAmmo -eq 35 -and (Read-Crew 'client').sharedAmmo -eq 35 }
        Send-Crew 'client' 'cache' 0 2
        Start-Sleep -Seconds 1
        Wait-Crew 'consumed shared ammo cannot be farmed from empty cache' { (Read-Crew 'host').sharedAmmo -eq 35 }
        Send-Crew 'host' 'place_car' 1
        Send-Crew 'client' 'seat' 0
        Wait-Crew 'client boards as driver for crew actions' { (Read-Crew 'client').seat -eq 0 }
        Send-Crew 'host' 'place_car' 0
        Send-Crew 'host' 'seat' 1
        Wait-Crew 'host boards as navigator for crew actions' { (Read-Crew 'host').seat -eq 1 }
        Send-Crew 'host' 'fixture' 1 3
        Start-Sleep -Milliseconds 500
        Send-Crew 'client' 'crew_action' 0 1
        Wait-Crew 'driver repair request rejected without spending materials' { (Read-Crew 'client').feedback -like '*штурман*' -and (Read-Crew 'host').hull -eq 50 -and (Read-Crew 'host').bolts -eq 12 }
        Send-Crew 'host' 'crew_action' 0 2
        Wait-Crew 'navigator heals wounded driver from shared medkit' { (Read-Crew 'client').health -eq 80 -and (Read-Crew 'host').medkits -eq 2 -and (Read-Crew 'client').medkits -eq 2 }
        Start-Sleep -Milliseconds 650
        Send-Crew 'host' 'crew_action' 0 2
        Wait-Crew 'crew healing capped at 100 HP' { (Read-Crew 'client').health -eq 100 -and (Read-Crew 'host').medkits -eq 1 }
        Start-Sleep -Milliseconds 650
        Send-Crew 'host' 'fixture' 0 6
        Start-Sleep -Seconds 2
        Send-Crew 'client' 'input' 0 0 .2
        Send-Crew 'host' 'fixture' 0 8
        Start-Sleep -Milliseconds 150
        Send-Crew 'host' 'crew_action' 0 1
        Wait-Crew 'navigator cannot repair while car is moving' { (Read-Crew 'host').feedback -like '*остановите*' -and (Read-Crew 'host').bolts -eq 12 -and (Read-Crew 'host').hull -eq 50 }
        Send-Crew 'client' 'input'
        Send-Crew 'host' 'fixture' 0 5
        Start-Sleep -Seconds 4
        Send-Crew 'host' 'crew_action' 0 1
        Wait-Crew 'navigator repairs hull and spends exactly two bolts' { (Read-Crew 'host').hull -eq 75 -and (Read-Crew 'client').hull -eq 75 -and (Read-Crew 'host').bolts -eq 10 }
        Send-Crew 'host' 'crew_action' 0 1
        Start-Sleep -Seconds 1
        Wait-Crew 'repair cooldown rejects duplicate command' { (Read-Crew 'host').hull -eq 75 -and (Read-Crew 'host').bolts -eq 10 }
        Start-Sleep -Seconds 4
        Send-Crew 'host' 'crew_action' 0 1
        Wait-Crew 'repair caps hull at 100' { (Read-Crew 'client').hull -eq 100 -and (Read-Crew 'client').bolts -eq 8 }
        Start-Sleep -Seconds 4
        Send-Crew 'host' 'crew_action' 0 1
        $script:healthyRetryAt = [DateTime]::UtcNow.AddSeconds(4)
        Wait-Crew 'healthy vehicle keeps repair materials' {
            $s = Read-Crew 'host'
            if ($s.feedback -like '*исправна*' -and $s.hull -eq 100 -and $s.bolts -eq 8) { return $true }
            # Repeating an inspection of an already healthy car cannot spend resources.
            # Resample its short feedback after a delayed frame or RPC acknowledgement.
            if ([DateTime]::UtcNow -ge $script:healthyRetryAt) {
                Send-Crew 'host' 'crew_action' 0 1
                $script:healthyRetryAt = [DateTime]::UtcNow.AddSeconds(4)
            }
            return $false
        }
        Send-Crew 'host' 'fixture' 1 7
        Send-Crew 'host' 'fixture' 0 6
        Start-Sleep -Seconds 2
        $engineStartZ = (Read-Crew 'host').vehiclePosition.z
        Send-Crew 'client' 'input' 0 0 .8
        Start-Sleep -Seconds 2
        Wait-Crew 'disabled engine rejects driver acceleration' { (Read-Crew 'host').speed -lt .5 -and [Math]::Abs((Read-Crew 'host').vehiclePosition.z - $engineStartZ) -lt .5 }
        Send-Crew 'client' 'input'
        Send-Crew 'host' 'fixture' 0 5
        Start-Sleep -Seconds 4
        Send-Crew 'host' 'crew_action' 0 1
        Wait-Crew 'disabled engine recovery consumes battery and bolts' { (Read-Crew 'host').hull -eq 25 -and (Read-Crew 'client').bolts -eq 6 -and (Read-Crew 'client').batteries -eq 0 }
        Send-Crew 'host' 'fixture' 1 7
        Start-Sleep -Seconds 4
        Send-Crew 'host' 'crew_action' 0 1
        Wait-Crew 'missing recovery battery keeps remaining materials' { (Read-Crew 'host').feedback -like '*АКБ*' -and (Read-Crew 'host').bolts -eq 6 -and (Read-Crew 'host').hull -eq 0 }
        Send-Crew 'host' 'fixture' 1 3
        Send-Crew 'host' 'capture'
        Send-Crew 'client' 'capture'
        Start-Sleep -Seconds 2
        Copy-Item (Join-Path $run 'host.png') (Join-Path $run 'navigator-supplies.png')
        Copy-Item (Join-Path $run 'client.png') (Join-Path $run 'driver-supplies.png')
    }
    }
    Send-Crew 'host' 'capture'
    Send-Crew 'client' 'capture'
    Start-Sleep -Seconds 2
    if ($CombatChecks) {
        Send-Crew 'host' 'combat' 0 0
        Wait-Crew 'enemy history ready on both peers' { (Read-Crew 'host').enemyHistory -and (Read-Crew 'client').enemyHistory -and (Read-Crew 'host').enemyId -eq (Read-Crew 'client').enemyId }
        Wait-Crew 'replica never runs navigation' { !(Read-Crew 'client').enemyAgent }
        $initialEnemyHealth = (Read-Crew 'host').enemyHealth
        Send-Crew 'client' 'local_enemy_damage'
        Start-Sleep -Seconds 1
        Wait-Crew 'client local damage cannot alter enemy health' { (Read-Crew 'host').enemyHealth -eq $initialEnemyHealth -and (Read-Crew 'client').enemyHealth -eq $initialEnemyHealth }
        Send-Crew 'client' 'aim_fire' 0 2
        Start-Sleep -Seconds 1
        Wait-Crew 'forged shot origin rejected without spending ammo' { (Read-Crew 'client').ammo -eq 7 -and (Read-Crew 'host').enemyHealth -eq $initialEnemyHealth }
        Send-Crew 'host' 'combat' 0 1
        Wait-Crew 'host AI chases actual remote player' { (Read-Crew 'host').enemyState -in @(1,2,3) }
        Wait-Crew 'enemy positions converge during chase' {
            $a=(Read-Crew 'host').enemyPosition; $b=(Read-Crew 'client').enemyPosition
            [Math]::Abs($a.x-$b.x)+[Math]::Abs($a.z-$b.z) -lt .8 -and (Read-Crew 'client').enemyState -in @(1,2,3,4)
        }
        Send-Crew 'host' 'combat' 0 2
        Wait-Crew 'melee windup replicated' { (Read-Crew 'client').enemyState -eq 3 }
        Send-Crew 'host' 'combat' 0 3
        $beforeDodge=(Read-Crew 'client').health
        Start-Sleep -Seconds 1
        Wait-Crew 'leaving melee reach cancels impact' { (Read-Crew 'client').health -eq $beforeDodge }
        Send-Crew 'host' 'combat' 0 2
        Wait-Crew 'server melee damages remote player' { (Read-Crew 'client').health -lt $beforeDodge -and (Read-Crew 'client').enemyHits -gt 0 }
        Send-Crew 'host' 'combat' 0 4
        Wait-Crew 'zombie melee reduces shared vehicle hull' { (Read-Crew 'host').hull -lt 100 -and (Read-Crew 'client').hull -lt 100 }
        Send-Crew 'host' 'combat' 0 0
        Start-Sleep -Seconds 1
        Send-Crew 'client' 'burst_fire'
        Wait-Crew 'body shot damage and ammo are authoritative' { (Read-Crew 'host').enemyHealth -eq [Math]::Max(0,$initialEnemyHealth-35) -and (Read-Crew 'client').enemyHealth -eq (Read-Crew 'host').enemyHealth -and (Read-Crew 'client').ammo -eq 6 }
        Send-Crew 'client' 'aim_fire' 0 1
        Wait-Crew 'headshot kills enemy on both peers' { (Read-Crew 'host').enemyHealth -le 0 -and (Read-Crew 'client').enemyHealth -le 0 -and !(Read-Crew 'client').enemyCollider }
        Wait-Crew 'corpse falls on both peers before despawn' { (Read-Crew 'host').enemyVisualAngle -gt 70 -and (Read-Crew 'client').enemyVisualAngle -gt 70 }
        Send-Crew 'host' 'capture'; Send-Crew 'client' 'capture'
        Start-Sleep -Seconds 3
        Copy-Item (Join-Path $run 'host.png') (Join-Path $run 'combat-host.png')
        Copy-Item (Join-Path $run 'client.png') (Join-Path $run 'combat-client.png')
        Send-Crew 'client' 'leave'
        Wait-Crew 'combat client disconnects' { !(Read-Crew 'client').connected }
        Send-Crew 'client' 'join'
        Wait-Crew 'late join preserves enemy death and HUD' { (Read-Crew 'client').connected -and (Read-Crew 'client').enemyHistory -and (Read-Crew 'client').enemyHealth -le 0 -and !(Read-Crew 'client').enemyCollider -and (Read-Crew 'client').supplyHudVisible } 90
    }
    $errors = Select-String -Path (Join-Path $run '*.log') -Pattern 'Exception:|GameObject has multiple AudioSources|\[Netcode\].*(Error|Failed)'
    if ($errors) { throw ('FAIL runtime errors: ' + ($errors | Select-Object -First 1).Line) }
    $checks.Add('PASS no runtime exceptions')
    $checks.Add('RESULT PASS')
} catch {
    $checks.Add($_.Exception.Message)
    Write-Output $_.Exception.Message
    Send-Crew 'host' 'capture'
    Send-Crew 'client' 'capture'
    Start-Sleep -Seconds 2
} finally {
    $checks | Set-Content -LiteralPath (Join-Path $run 'results.txt') -Encoding utf8
    foreach ($p in $processes) { if (!$p.HasExited) { Stop-Process -Id $p.Id } }
    Write-Output "REPORT $run"
}
if ($checks -notcontains 'RESULT PASS') { exit 1 }
