param([string]$ProjectRoot = 'W:\BSUIR\DIPLOOM', [string]$BuildPath = 'Builds/SteamMilestone/RogueDriveCoop.exe', [int]$TestPort = 17777)
$ErrorActionPreference = 'Stop'
$run = Join-Path $ProjectRoot ('Temp/CoopBunker/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$exe = Join-Path $ProjectRoot $BuildPath
if (Get-NetUDPEndpoint -LocalPort $TestPort -ErrorAction SilentlyContinue) { throw "Test port $TestPort is already in use." }
$assembly = Join-Path (Split-Path $exe) 'RogueDriveCoop_Data/Managed/Assembly-CSharp.dll'
@{ build=$exe; testPort=$TestPort; assemblySha256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash; startedUtc=[DateTime]::UtcNow.ToString('o') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'build.json') -Encoding utf8
$processes = @()
$checks = [System.Collections.Generic.List[string]]::new()
$script:sequence = 0
function Send-Crew($role, $action, $station = 0, $seat = 0, $throttle = 0, $placement = 0) {
    $script:sequence++
    $command = @{sequence=$script:sequence; action=$action; station=$station; seat=$seat; throttle=$throttle; yaw=$placement; brake=($throttle -eq 0)} | ConvertTo-Json
    [System.IO.File]::WriteAllText((Join-Path $run "$role.command.json"), $command)
}
function Read-Crew($role) {
    for ($attempt = 0; $attempt -lt 5; $attempt++) {
        try {
            $snapshot = Get-Content -LiteralPath (Join-Path $run "$role.snapshot.json") -Raw | ConvertFrom-Json
            if ($null -ne $snapshot) { return $snapshot }
        } catch { }
        Start-Sleep -Milliseconds 25
    }
    return $null
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
    $script:processes += Start-Process -FilePath $exe -ArgumentList @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-coop-test-port',$TestPort,'-coop-probe',$prefix,'-logFile',"$prefix.log") -WindowStyle Hidden -PassThru
}
try {
    Start-Crew 'host'
    Wait-Crew 'host application ready' { $null -ne (Read-Crew 'host') }
    Send-Crew 'host' 'host'
    Wait-Crew 'host connected' { (Read-Crew 'host').connected }
    Start-Crew 'client'
    Wait-Crew 'client application ready' { $null -ne (Read-Crew 'client') }
    Send-Crew 'client' 'join'
    Wait-Crew 'two peers connected' { (Read-Crew 'host').clientConnected -and (Read-Crew 'client').clientConnected }
    Send-Crew 'host' 'ready'
    Send-Crew 'client' 'ready'
    Wait-Crew 'shared quest and vehicle spawned on both peers' { (Read-Crew 'host').questSpawned -and (Read-Crew 'client').questSpawned -and (Read-Crew 'client').players -eq 2 } 90
    Wait-Crew 'exactly one crew canvas per peer' { (Read-Crew 'host').crewCanvases -eq 1 -and (Read-Crew 'client').crewCanvases -eq 1 }
    Send-Crew 'client' 'station' 0
    Start-Sleep -Seconds 1
    if ((Read-Crew 'host').generator) { throw 'FAIL generator accepted from afar' }
    $checks.Add('PASS remote station rejected from afar')
    foreach ($step in @(@{station=0; field='generator'}, @{station=1; field='wheel'}, @{station=2; field='battery'}, @{station=3; field='fuel'}, @{station=4; field='keys'}, @{station=5; field='gates'})) {
        $field = $step.field
        # Approach from different sides: scene furniture can obstruct a valid station.
        for ($side = 0; $side -lt 12; $side++) {
            Send-Crew 'host' 'place_station' $step.station 1 0 $side
            Start-Sleep -Seconds 1
            Send-Crew 'client' 'station' $step.station
            Start-Sleep -Seconds 1
            if ((Read-Crew 'host').$field -and (Read-Crew 'client').$field) { break }
        }
        Wait-Crew "client action replicated: $field" { (Read-Crew 'host').$field -and (Read-Crew 'client').$field }
        if ($field -eq 'fuel') {
            Send-Crew 'client' 'station' $step.station
            Wait-Crew 'fuel capped at required 15 liters' { (Read-Crew 'host').fuel -eq 15 -and (Read-Crew 'client').fuel -eq 15 }
        }
    }
    Send-Crew 'host' 'capture'
    Send-Crew 'client' 'capture'
    Start-Sleep -Seconds 2
    Copy-Item -LiteralPath (Join-Path $run 'host.png') -Destination (Join-Path $run 'bunker-host.png')
    Copy-Item -LiteralPath (Join-Path $run 'client.png') -Destination (Join-Path $run 'bunker-client.png')
    Send-Crew 'client' 'leave'
    Wait-Crew 'client leaves prepared bunker' { (Read-Crew 'client').scene -eq 'MainMenuScene' -and !(Read-Crew 'client').connected }
    Send-Crew 'client' 'join'
    Wait-Crew 'late join restores preparation' { $c = Read-Crew 'client'; $c.questSpawned -and $c.generator -and $c.wheel -and $c.battery -and $c.keys -and $c.gates -and $c.fuel -eq 15 }
    Send-Crew 'host' 'place_car' 0 0
    Start-Sleep -Seconds 1
    Send-Crew 'host' 'seat' 0 0
    Wait-Crew 'host becomes driver' { (Read-Crew 'host').seat -eq 0 }
    Send-Crew 'host' 'place_car' 0 1
    Start-Sleep -Seconds 1
    Send-Crew 'client' 'seat' 0 1
    Wait-Crew 'client becomes navigator' { (Read-Crew 'client').seat -eq 1 }
    $vehicleId = (Read-Crew 'host').vehicleId
    $hostId = (Read-Crew 'host').playerId
    $clientId = (Read-Crew 'client').playerId
    if (!$vehicleId -or !$hostId -or !$clientId) { throw 'FAIL missing actor snapshot before departure' }
    @{ vehicleId=$vehicleId; hostPlayerId=$hostId; clientPlayerId=$clientId } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'before-departure.json')
    Send-Crew 'host' 'damage'
    Wait-Crew 'damage applied before departure' { (Read-Crew 'host').health -eq 85 }
    Send-Crew 'host' 'drive' 0 0 1
    Wait-Crew 'both peers reach road' { (Read-Crew 'host').departed -and (Read-Crew 'client').departed } 90
    Send-Crew 'host' 'stop'
    Wait-Crew 'same actors and preparation after departure' {
        $h = Read-Crew 'host'; $c = Read-Crew 'client'
        $h.scene -eq 'Stage1_Outskirts' -and $c.scene -eq 'Stage1_Outskirts' -and $h.departed -and $c.departed -and !$h.transitioning -and
        $h.vehicleId -eq $vehicleId -and $c.vehicleId -eq $vehicleId -and $h.playerId -eq $hostId -and $c.playerId -eq $clientId -and
        $h.seat -eq 0 -and $c.seat -eq 1 -and $h.fuel -eq 15 -and $c.fuel -eq 15 -and $h.health -eq 85
    }
    Send-Crew 'host' 'capture'
    Send-Crew 'client' 'capture'
    Wait-Crew 'road has one vehicle, camera, listener, crew canvas per peer' {
        $h = Read-Crew 'host'; $c = Read-Crew 'client'
        $h.vehicles -eq 1 -and $c.vehicles -eq 1 -and $h.cameraCount -eq 1 -and $c.cameraCount -eq 1 -and
        $h.listenerCount -eq 1 -and $c.listenerCount -eq 1 -and $h.crewCanvases -eq 1 -and $c.crewCanvases -eq 1
    }
    Start-Sleep -Seconds 2
    $errors = Select-String -Path (Join-Path $run '*.log') -Pattern 'Exception:|GameObject has multiple AudioSources|\[Netcode\].*(Error|Failed)'
    if ($errors) { throw ('FAIL runtime errors: ' + ($errors | Select-Object -First 1).Line) }
    $checks.Add('PASS no runtime exceptions')
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
