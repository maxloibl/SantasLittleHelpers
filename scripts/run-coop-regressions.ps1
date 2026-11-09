param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('coop-smoke', 'four-player', 'gift-regression', 'capture-all-caught')]
    [string] $Scenario,

    [Parameter(Mandatory = $true)]
    [string] $BuildPath,

    [string] $LogRoot = 'C:\tmp\slh14-regressions',
    [int] $Port = 7784,
    [int] $RunSeconds = 50
)

$ErrorActionPreference = 'Stop'
$resolvedBuild = (Resolve-Path -LiteralPath $BuildPath).Path
if ([IO.Path]::GetExtension($resolvedBuild) -ne '.exe') { throw "Expected a Windows player .exe: $resolvedBuild" }
$runId = '{0}-{1:yyyyMMdd-HHmmss}' -f $Scenario, (Get-Date)
$runDirectory = Join-Path $LogRoot $runId
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$clientCount = switch ($Scenario) {
    'coop-smoke' { 1 }
    'four-player' { 3 }
    'gift-regression' { 1 }
    'capture-all-caught' { 1 }
}
$extraFlags = switch ($Scenario) {
    'coop-smoke' { @('--coop-smoke') }
    'four-player' { @() }
    'gift-regression' { @() }
    'capture-all-caught' { @('--capture-auto-reset', '--capture-wait-for-client') }
}
$effectivePort = switch ($Scenario) {
    'capture-all-caught' { 7780 }
    'gift-regression' { 7777 }
    default { $Port }
}
$started = [Collections.Generic.List[Diagnostics.Process]]::new()

try {
    $hostArguments = @('-batchmode', '-nographics', '-logFile', (Join-Path $runDirectory 'host.log'), '--host') + $extraFlags
    if ($Scenario -ne 'capture-all-caught') { $hostArguments += "--port=$effectivePort" }
    if ($Scenario -eq 'coop-smoke') { $hostArguments += '--shutdown-after=40' }
    $hostProcess = Start-Process -FilePath $resolvedBuild -ArgumentList $hostArguments -PassThru -WindowStyle Hidden
    $started.Add($hostProcess)
    Start-Sleep -Seconds 2

    for ($index = 1; $index -le $clientCount; $index++) {
        $clientArguments = @('-batchmode', '-nographics', '-logFile', (Join-Path $runDirectory "client$index.log"), '--client') + $extraFlags
        if ($Scenario -ne 'capture-all-caught') { $clientArguments += "--port=$effectivePort" }
        if ($Scenario -eq 'coop-smoke') { $clientArguments += '--shutdown-after=35' }
        $clientProcess = Start-Process -FilePath $resolvedBuild -ArgumentList $clientArguments -PassThru -WindowStyle Hidden
        $started.Add($clientProcess)
        Start-Sleep -Milliseconds 1200
    }

    Write-Output "Scenario=$Scenario Players=$($clientCount + 1) Port=$effectivePort Build=$resolvedBuild Logs=$runDirectory"
    Start-Sleep -Seconds $RunSeconds
}
finally {
    foreach ($process in $started) {
        $process.Refresh()
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    }
}

$logFiles = Get-ChildItem -LiteralPath $runDirectory -Filter '*.log'
$allLogs = ($logFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$requirements = switch ($Scenario) {
    'coop-smoke' { @('COOP_SMOKE_HEAVY_TWO_GRIPS_PASS', 'COOP_SMOKE_CAPTURE_PASS', 'COOP_SMOKE_CAPTURE_GRIP_RELEASE_PASS', 'COOP_SMOKE_CAPTURED_GRAB_REJECT_PASS', 'COOP_SMOKE_CAPTURE_RESCUE_PASS', 'COOP_GIFT_DELIVERED', 'COOP_SMOKE_RESTART_PASS') }
    'four-player' { @('COOP_PLAYER_START owner=0', 'COOP_PLAYER_START owner=1', 'COOP_PLAYER_START owner=2', 'COOP_PLAYER_START owner=3', 'MISSION_PLAYER owner=3 localView=False') }
    'gift-regression' { @('GIFT_TEST_RESULT', 'GIFT_TEST_DISCONNECT', 'GIFT_TEST_REGRAB', 'GIFT_TEST_SINGLE_SLOT_RESULT') }
    'capture-all-caught' { @('CAPTURE_ROUND_LOST value=True', 'CAPTURE_HOST_RESET complete=true', 'CAPTURE_SMOKE_SECOND_ROUND_START') }
}
$missing = @($requirements | Where-Object { $allLogs -notmatch [regex]::Escape($_) })
if ($missing.Count -gt 0) {
    Write-Output "RESULT=INCOMPLETE missing=$($missing -join '; ')"
    exit 2
}
if ($Scenario -eq 'four-player') {
    $clientStarts = [regex]::Matches($allLogs, 'COOP_PLAYER_START owner=[123] position=\(([^)]+)\)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    if ($clientStarts.Count -lt 3) {
        Write-Output "RESULT=FAIL duplicate client spawn positions: $($clientStarts -join '; ')"
        exit 1
    }
}
if ($Scenario -eq 'gift-regression') {
    $slotResults = [regex]::Matches($allLogs, 'GIFT_TEST_SINGLE_SLOT_RESULT owner=(\d+) hasSlot=(True|False) gripCount=(\d+)')
    $owners = @($slotResults | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $slotValues = @($slotResults | ForEach-Object { $_.Groups[2].Value } | Sort-Object -Unique)
    $singleSlotCounts = @($slotResults | ForEach-Object { $_.Groups[3].Value } | Sort-Object -Unique)
    if ($owners.Count -ne 2 -or $slotValues.Count -ne 2 -or $singleSlotCounts.Count -ne 1 -or $singleSlotCounts[0] -ne '1') {
        Write-Output 'RESULT=FAIL distinct-owner single-slot arbitration was not demonstrated'
        exit 1
    }
}
Write-Output 'RESULT=PASS required log evidence present'
