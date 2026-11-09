param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('coop-smoke', 'four-player', 'gift-regression', 'capture-all-caught')]
    [string] $Scenario,

    [Parameter(Mandatory = $true)]
    [string] $BuildPath,

    [string] $LogRoot = 'C:\tmp\slh14-regressions',
    [ValidateRange(1024, 65535)]
    [int] $Port = 7784,
    [ValidateRange(10, 600)]
    [int] $RunSeconds = 50
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'coop-regression-log-checks.ps1')
$resolvedBuild = (Resolve-Path -LiteralPath $BuildPath).Path
if ([IO.Path]::GetExtension($resolvedBuild) -ne '.exe') { throw "Expected a Windows player .exe: $resolvedBuild" }
$resolvedLogRoot = [IO.Path]::GetFullPath($LogRoot)
$runId = '{0}-{1:yyyyMMdd-HHmmss-fff}-{2}' -f $Scenario, [DateTime]::UtcNow, [guid]::NewGuid().ToString('N')
$runDirectory = Join-Path $resolvedLogRoot $runId
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$clientCount = switch ($Scenario) {
    'coop-smoke' { 1 }
    'four-player' { 3 }
    'gift-regression' { 1 }
    'capture-all-caught' { 1 }
}
$extraFlags = switch ($Scenario) {
    'coop-smoke' { @('--coop-smoke') }
    'four-player' { @('--coop-four-player-reset-smoke') }
    'gift-regression' { @() }
    'capture-all-caught' { @('--capture-auto-reset', '--capture-wait-for-client') }
}
$effectivePort = switch ($Scenario) {
    'capture-all-caught' { 7780 }
    'gift-regression' { 7777 }
    default { $Port }
}
$started = [Collections.Generic.List[Diagnostics.Process]]::new()
$forcedStopIds = [Collections.Generic.HashSet[int]]::new()
$runFailures = [Collections.Generic.List[string]]::new()

function ConvertTo-WindowsArgument([string] $Value) {
    $builder = [Text.StringBuilder]::new()
    [void]$builder.Append('"')
    $backslashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') { $backslashes++; continue }
        if ($character -eq '"') {
            [void]$builder.Append(('\' * ($backslashes * 2 + 1)))
            [void]$builder.Append('"')
            $backslashes = 0
            continue
        }
        if ($backslashes -gt 0) { [void]$builder.Append(('\' * $backslashes)); $backslashes = 0 }
        [void]$builder.Append($character)
    }
    if ($backslashes -gt 0) { [void]$builder.Append(('\' * ($backslashes * 2))) }
    [void]$builder.Append('"')
    $builder.ToString()
}

function Start-HiddenPlayer([string[]] $Arguments) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $resolvedBuild
    $startInfo.Arguments = (($Arguments | ForEach-Object { ConvertTo-WindowsArgument $_ }) -join ' ')
    $startInfo.WorkingDirectory = Split-Path -Parent $resolvedBuild
    $startInfo.UseShellExecute = $true
    $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $process = [Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) { throw "Could not start $resolvedBuild" }
    $started.Add($process)
    $process
}

try {
    $hostArguments = @('-batchmode', '-nographics', '-logFile', (Join-Path $runDirectory 'host.log'), '--host') + $extraFlags
    if ($Scenario -ne 'capture-all-caught') { $hostArguments += "--port=$effectivePort" }
    if ($Scenario -in @('coop-smoke', 'four-player')) { $hostArguments += "--shutdown-after=$([Math]::Max(15, $RunSeconds - 5))" }
    $hostProcess = Start-HiddenPlayer $hostArguments
    Write-Output "PROCESS role=host pid=$($hostProcess.Id)"
    Start-Sleep -Seconds 2

    for ($index = 1; $index -le $clientCount; $index++) {
        $clientArguments = @('-batchmode', '-nographics', '-logFile', (Join-Path $runDirectory "client$index.log"), '--client') + $extraFlags
        if ($Scenario -ne 'capture-all-caught') { $clientArguments += "--port=$effectivePort" }
        if ($Scenario -in @('coop-smoke', 'four-player')) { $clientArguments += "--shutdown-after=$([Math]::Max(15, $RunSeconds - 8))" }
        $clientProcess = Start-HiddenPlayer $clientArguments
        Write-Output "PROCESS role=client pid=$($clientProcess.Id)"
        Start-Sleep -Milliseconds 1200
    }

    Write-Output "Scenario=$Scenario Players=$($clientCount + 1) Port=$effectivePort Build=$resolvedBuild Logs=$runDirectory"
    $deadline = [DateTime]::UtcNow.AddSeconds($RunSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $activeCount = 0
        foreach ($process in $started) {
            $process.Refresh()
            if (-not $process.HasExited) { $activeCount++ }
        }
        if ($activeCount -eq 0) { break }
        Start-Sleep -Seconds 1
    }
}
finally {
    foreach ($process in $started) {
        $process.Refresh()
        if (-not $process.HasExited) {
            [void]$forcedStopIds.Add($process.Id)
            try { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
            catch { $runFailures.Add("Could not stop own process $($process.Id): $($_.Exception.Message)") }
        }
    }
}

foreach ($process in $started) {
    $process.Refresh()
    if (-not $process.HasExited) {
        $runFailures.Add("Own process $($process.Id) remained alive after cleanup")
        continue
    }
    if (-not $forcedStopIds.Contains($process.Id) -and $process.ExitCode -ne 0) {
        $runFailures.Add("Natural process exit was $($process.ExitCode) for pid $($process.Id)")
    }
    $closure = if ($forcedStopIds.Contains($process.Id)) { 'harness-stop' } else { "natural-exit=$($process.ExitCode)" }
    Write-Output "PROCESS_CLOSED pid=$($process.Id) result=$closure"
}

$logFiles = Get-ChildItem -LiteralPath $runDirectory -Filter '*.log'
if ($logFiles.Count -eq 0) { $runFailures.Add('no Unity log files were created') }
$allLogs = ($logFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$requirements = switch ($Scenario) {
    'coop-smoke' { @('COOP_SMOKE_HEAVY_TWO_GRIPS_PASS', 'COOP_SMOKE_CAPTURE_PASS', 'COOP_SMOKE_CAPTURE_GRIP_RELEASE_PASS', 'COOP_SMOKE_CAPTURED_GRAB_REJECT_PASS', 'COOP_SMOKE_CAPTURE_RESCUE_PASS', 'COOP_GIFT_DELIVERED', 'COOP_SMOKE_RESTART_PASS') }
    'four-player' { @('COOP_FOUR_PLAYER_RESET_SMOKE started=true players=4') }
    'gift-regression' { @('GIFT_TEST_SINGLE_SLOT_RESULT') }
    'capture-all-caught' { @('CAPTURE_ROUND_LOST value=True', 'CAPTURE_HOST_RESET complete=true', 'CAPTURE_SMOKE_SECOND_ROUND_START') }
}
$missing = @($requirements | Where-Object { $allLogs -notmatch [regex]::Escape($_) })
if ($missing.Count -gt 0) { $runFailures.Add("Required marker missing: $($missing -join '; ')") }

if ($Scenario -eq 'gift-regression') {
    if ([string]::IsNullOrWhiteSpace($allLogs)) { $runFailures.Add('no gift log content was available') }
    $giftResult = Test-GiftRegressionLog -LogText $allLogs
    foreach ($issue in $giftResult.Issues) { $runFailures.Add($issue) }
}
if ($Scenario -eq 'four-player') {
    $hostLogPath = Join-Path $runDirectory 'host.log'
    if (-not (Test-Path -LiteralPath $hostLogPath -PathType Leaf)) { $runFailures.Add('host log file is missing') }
    else {
        $fourPlayerResult = Test-FourPlayerLog -HostLog (Get-Content -LiteralPath $hostLogPath -Raw)
        foreach ($issue in $fourPlayerResult.Issues) { $runFailures.Add($issue) }
    }
}

if ($runFailures.Count -gt 0) {
    foreach ($failure in $runFailures) { Write-Output "FAILURE=$failure" }
    Write-Output 'RESULT=FAIL'
    exit 1
}
Write-Output 'RESULT=PASS required values and process results verified'
