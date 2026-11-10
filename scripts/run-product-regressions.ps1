[CmdletBinding()]
param(
    [ValidateSet('fixture', 'player')]
    [string] $Mode = 'fixture',

    [string[]] $Case,

    [string] $BuildPath,

    [string] $LogRoot,

    [string] $ProfileRoot,

    [ValidateRange(1024, 65535)]
    [int] $Port = 7796,

    [ValidateRange(10, 600)]
    [int] $TimeoutSeconds = 60,

    [switch] $KeepProfiles,

    [switch] $ListCases
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'product-regression-cases.ps1')
. (Join-Path $PSScriptRoot 'product-regression-log-checks.ps1')

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$script:started = [Collections.Generic.List[object]]::new()
$script:runFailures = [Collections.Generic.List[string]]::new()

function Resolve-WorktreePath {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Label
    )

    if ([string]::IsNullOrWhiteSpace($Path)) { throw "$Label requires a non-empty path." }
    $candidate = if ([IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $repoRoot $Path }
    $full = [IO.Path]::GetFullPath($candidate)
    $separator = [IO.Path]::DirectorySeparatorChar
    $inside = $full.Equals($repoRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $full.StartsWith($repoRoot.TrimEnd('\', '/') + $separator, [StringComparison]::OrdinalIgnoreCase)
    if (-not $inside) { throw "$Label must stay inside the worktree: $full" }

    $ignored = $false
    foreach ($name in @('Logs', 'Builds')) {
        $prefix = Join-Path $repoRoot $name
        if ($full.Equals($prefix, [StringComparison]::OrdinalIgnoreCase) -or
            $full.StartsWith($prefix + $separator, [StringComparison]::OrdinalIgnoreCase)) {
            $ignored = $true
            break
        }
    }
    if (-not $ignored) { throw "$Label must be under Logs/ or Builds/ inside the worktree: $full" }
    $full
}

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

function Start-HiddenPlayer {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $Role,
        [Parameter(Mandatory = $true)][string] $CaseName,
        [Parameter(Mandatory = $true)][string] $LogPath
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $resolvedBuild
    $startInfo.Arguments = (($Arguments | ForEach-Object { ConvertTo-WindowsArgument $_ }) -join ' ')
    $startInfo.WorkingDirectory = Split-Path -Parent $resolvedBuild
    $startInfo.UseShellExecute = $true
    $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $process = [Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) { throw "Could not start $resolvedBuild" }
    $record = [pscustomobject]@{
        Process = $process
        Role    = $Role
        Case    = $CaseName
        LogPath = $LogPath
        Forced  = $false
    }
    $script:started.Add($record)
    $record
}

function Start-CaseProcess {
    param(
        [Parameter(Mandatory = $true)][string] $CaseName,
        [Parameter(Mandatory = $true)][string] $Role,
        [Parameter(Mandatory = $true)][string] $ProfileDir,
        [Parameter(Mandatory = $true)][string] $LogPath,
        [Parameter(Mandatory = $true)][int] $CasePort,
        [string[]] $Extras = @()
    )

    New-Item -ItemType Directory -Path $ProfileDir -Force | Out-Null
    New-Item -ItemType Directory -Path (Split-Path -Parent $LogPath) -Force | Out-Null
    $arguments = @(
        '-batchmode',
        '-nographics',
        '-logFile', $LogPath,
        "--product-smoke=$CaseName",
        "--profile=$ProfileDir",
        "--port=$CasePort"
    )
    if ($Role -eq 'host') { $arguments += '--host' }
    elseif ($Role -like 'guest*') { $arguments += '--client' }
    if ($Extras.Count -gt 0) { $arguments += $Extras }
    Start-HiddenPlayer -Arguments $arguments -Role $Role -CaseName $CaseName -LogPath $LogPath
}

function Wait-ForCaseProcess {
    param(
        [Parameter(Mandatory = $true)] $Record,
        [Parameter(Mandatory = $true)][datetime] $Deadline
    )

    while ($true) {
        $Record.Process.Refresh()
        if ($Record.Process.HasExited) { return $true }
        if ([DateTime]::UtcNow -ge $Deadline) { return $false }
        [void]$Record.Process.WaitForExit(250)
    }
}

function Stop-CaseProcess {
    param(
        [Parameter(Mandatory = $true)] $Record,
        [Parameter(Mandatory = $true)][string] $Reason
    )

    $Record.Process.Refresh()
    if ($Record.Process.HasExited) { return }
    $Record.Forced = $true
    try {
        $Record.Process.Kill()
        [void]$Record.Process.WaitForExit(5000)
        Write-Host "PROCESS_STOP role=$($Record.Role) case=$($Record.Case) pid=$($Record.Process.Id) reason=$Reason"
    }
    catch {
        $script:runFailures.Add("Could not stop own process $($Record.Process.Id) for $($Record.Case): $($_.Exception.Message)")
    }
}

function Get-CaseLogRoles {
    param([Parameter(Mandatory = $true)][string] $CaseName)

    switch ($CaseName) {
        'solo-transition' { @('solo') }
        'duo-transition' { @('host', 'guest') }
        'four-transition' { @('host', 'guest1', 'guest2', 'guest3') }
        'save-roundtrip' { @('solo-run1', 'solo-run2') }
        'no-client-write' { @('host', 'guest') }
        'invalid-join' { @('guest') }
        'rejoin' { @('host', 'guest1', 'guest2') }
        'full-hut' { @('host', 'guest1', 'guest2', 'guest3', 'guest4') }
        'host-loss' { @('host', 'guest') }
    }
}

function Get-ProductProcessCount {
    param([Parameter(Mandatory = $true)][string] $CaseName)

    switch ($CaseName) {
        'solo-transition' { 1 }
        'duo-transition' { 2 }
        'four-transition' { 4 }
        'save-roundtrip' { 2 }
        'no-client-write' { 2 }
        'invalid-join' { 1 }
        'rejoin' { 3 }
        'full-hut' { 5 }
        'host-loss' { 2 }
        default { 0 }
    }
}

function Invoke-PlayerCase {
    param(
        [Parameter(Mandatory = $true)][string] $CaseName,
        [Parameter(Mandatory = $true)][int] $CasePort,
        [Parameter(Mandatory = $true)][string] $CaseLogDir,
        [Parameter(Mandatory = $true)][string] $CaseProfileDir
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $sentinelUnchanged = $true
    $sentinelCheck = $null
    $logPaths = @{}
    $profileFor = {
        param([string] $Role)
        Join-Path $CaseProfileDir $Role
    }

    switch ($CaseName) {
        'solo-transition' {
            $logPaths['solo'] = Join-Path $CaseLogDir 'solo.log'
            [void](Start-CaseProcess -CaseName $CaseName -Role 'solo' -ProfileDir (& $profileFor 'solo') -LogPath $logPaths['solo'] -CasePort $CasePort)
        }
        'save-roundtrip' {
            $sharedProfile = & $profileFor 'solo'
            $logPaths['solo-run1'] = Join-Path $CaseLogDir 'solo-run1.log'
            $record = Start-CaseProcess -CaseName $CaseName -Role 'solo' -ProfileDir $sharedProfile -LogPath $logPaths['solo-run1'] -CasePort $CasePort
            if (-not (Wait-ForCaseProcess -Record $record -Deadline $deadline)) {
                Stop-CaseProcess -Record $record -Reason 'save-roundtrip-first-timeout'
            }
            $logPaths['solo-run2'] = Join-Path $CaseLogDir 'solo-run2.log'
            [void](Start-CaseProcess -CaseName $CaseName -Role 'solo' -ProfileDir $sharedProfile -LogPath $logPaths['solo-run2'] -CasePort $CasePort)
        }
        'duo-transition' {
            $logPaths['host'] = Join-Path $CaseLogDir 'host.log'
            $logPaths['guest'] = Join-Path $CaseLogDir 'guest.log'
            [void](Start-CaseProcess -CaseName $CaseName -Role 'host' -ProfileDir (& $profileFor 'host') -LogPath $logPaths['host'] -CasePort $CasePort)
            Start-Sleep -Seconds 2
            [void](Start-CaseProcess -CaseName $CaseName -Role 'guest' -ProfileDir (& $profileFor 'guest') -LogPath $logPaths['guest'] -CasePort $CasePort -Extras @('--join=127.0.0.1'))
        }
        'four-transition' {
            $logPaths['host'] = Join-Path $CaseLogDir 'host.log'
            [void](Start-CaseProcess -CaseName $CaseName -Role 'host' -ProfileDir (& $profileFor 'host') -LogPath $logPaths['host'] -CasePort $CasePort)
            Start-Sleep -Seconds 2
            for ($index = 1; $index -le 3; $index++) {
                $key = "guest$index"
                $logPaths[$key] = Join-Path $CaseLogDir "$key.log"
                [void](Start-CaseProcess -CaseName $CaseName -Role $key -ProfileDir (& $profileFor $key) -LogPath $logPaths[$key] -CasePort $CasePort -Extras @('--join=127.0.0.1'))
                Start-Sleep -Milliseconds 1200
            }
        }
        'no-client-write' {
            $hostProfile = & $profileFor 'host'
            New-Item -ItemType Directory -Path $hostProfile -Force | Out-Null
            $sentinelPath = Join-Path $hostProfile 'product-storage-sentinel.txt'
            $sentinelValue = 'sentinel-' + [guid]::NewGuid().ToString('N')
            Set-Content -LiteralPath $sentinelPath -Value $sentinelValue -NoNewline
            $before = Get-Content -LiteralPath $sentinelPath -Raw
            $logPaths['host'] = Join-Path $CaseLogDir 'host.log'
            $logPaths['guest'] = Join-Path $CaseLogDir 'guest.log'
            [void](Start-CaseProcess -CaseName $CaseName -Role 'host' -ProfileDir $hostProfile -LogPath $logPaths['host'] -CasePort $CasePort)
            Start-Sleep -Seconds 2
            [void](Start-CaseProcess -CaseName $CaseName -Role 'guest' -ProfileDir (& $profileFor 'guest') -LogPath $logPaths['guest'] -CasePort $CasePort -Extras @('--join=127.0.0.1'))
            $sentinelCheck = @{ Path = $sentinelPath; Before = $before }
        }
        'invalid-join' {
            $logPaths['guest'] = Join-Path $CaseLogDir 'guest.log'
            [void](Start-CaseProcess -CaseName $CaseName -Role 'guest' -ProfileDir (& $profileFor 'guest') -LogPath $logPaths['guest'] -CasePort $CasePort -Extras @('--join=999.999.999.999'))
        }
        'rejoin' {
            $logPaths['host'] = Join-Path $CaseLogDir 'host.log'
            $logPaths['guest1'] = Join-Path $CaseLogDir 'guest1.log'
            $logPaths['guest2'] = Join-Path $CaseLogDir 'guest2.log'
            [void](Start-CaseProcess -CaseName $CaseName -Role 'host' -ProfileDir (& $profileFor 'host') -LogPath $logPaths['host'] -CasePort $CasePort)
            Start-Sleep -Seconds 2
            $leaveAfter = [Math]::Max(3, [Math]::Floor($TimeoutSeconds / 4))
            $first = Start-CaseProcess -CaseName $CaseName -Role 'guest1' -ProfileDir (& $profileFor 'guest1') -LogPath $logPaths['guest1'] -CasePort $CasePort -Extras @('--join=127.0.0.1', "--leave-after=$leaveAfter")
            if (-not (Wait-ForCaseProcess -Record $first -Deadline $deadline)) {
                Stop-CaseProcess -Record $first -Reason 'rejoin-leave-timeout'
            }
            [void](Start-CaseProcess -CaseName $CaseName -Role 'guest2' -ProfileDir (& $profileFor 'guest2') -LogPath $logPaths['guest2'] -CasePort $CasePort -Extras @('--join=127.0.0.1'))
        }
        'full-hut' {
            $logPaths['host'] = Join-Path $CaseLogDir 'host.log'
            [void](Start-CaseProcess -CaseName $CaseName -Role 'host' -ProfileDir (& $profileFor 'host') -LogPath $logPaths['host'] -CasePort $CasePort)
            Start-Sleep -Seconds 2
            for ($index = 1; $index -le 4; $index++) {
                $key = "guest$index"
                $logPaths[$key] = Join-Path $CaseLogDir "$key.log"
                [void](Start-CaseProcess -CaseName $CaseName -Role $key -ProfileDir (& $profileFor $key) -LogPath $logPaths[$key] -CasePort $CasePort -Extras @('--join=127.0.0.1'))
                Start-Sleep -Milliseconds 1200
            }
        }
        'host-loss' {
            $logPaths['host'] = Join-Path $CaseLogDir 'host.log'
            $logPaths['guest'] = Join-Path $CaseLogDir 'guest.log'
            $hostRecord = Start-CaseProcess -CaseName $CaseName -Role 'host' -ProfileDir (& $profileFor 'host') -LogPath $logPaths['host'] -CasePort $CasePort
            Start-Sleep -Seconds 2
            [void](Start-CaseProcess -CaseName $CaseName -Role 'guest' -ProfileDir (& $profileFor 'guest') -LogPath $logPaths['guest'] -CasePort $CasePort -Extras @('--join=127.0.0.1'))
            $hostKillAfter = [Math]::Max(3, [Math]::Min(15, [Math]::Floor($TimeoutSeconds / 3)))
            Start-Sleep -Seconds $hostKillAfter
            Stop-CaseProcess -Record $hostRecord -Reason 'host-loss'
        }
    }

    foreach ($record in @($script:started | Where-Object { $_.Case -eq $CaseName })) {
        if (-not (Wait-ForCaseProcess -Record $record -Deadline $deadline)) {
            Stop-CaseProcess -Record $record -Reason 'case-timeout'
        }
    }

    if ($null -ne $sentinelCheck) {
        $after = if (Test-Path -LiteralPath $sentinelCheck.Path -PathType Leaf) { Get-Content -LiteralPath $sentinelCheck.Path -Raw } else { $null }
        $sentinelUnchanged = ($null -ne $after -and $sentinelCheck.Before -eq $after)
    }

    $logs = @{}
    foreach ($key in (Get-CaseLogRoles -CaseName $CaseName)) {
        $path = $logPaths[$key]
        $logs[$key] = if (Test-Path -LiteralPath $path -PathType Leaf) { Get-Content -LiteralPath $path -Raw } else { '' }
    }
    [pscustomobject]@{ Logs = $logs; StorageSentinelUnchanged = $sentinelUnchanged }
}

$allCases = Get-ProductCaseCatalog
if ($ListCases) {
    foreach ($name in $allCases.Keys) {
        $definition = $allCases[$name]
        Write-Output ("CASE name={0} kind={1} players={2} processes={3} description={4}" -f `
                $definition.Name, $definition.Kind, $definition.Players, (Get-ProductProcessCount $name), $definition.Description)
    }
    exit 0
}

$selectedCases = if ($Case) {
    $names = [Collections.Generic.List[string]]::new()
    foreach ($name in $Case) {
        $definition = Get-ProductCaseDefinition -Name $name
        if ($definition.Kind -eq 'manual') {
            throw "Case '$name' is a manual GUI/LAN acceptance case; see docs/PRODUCT-TEST-MATRIX.md."
        }
        if (-not $names.Contains($name)) { $names.Add($name) }
    }
    @($names)
}
else {
    @(Get-ProductProcessSmokeCaseNames)
}

$resolvedLogRoot = Resolve-WorktreePath -Path $(if ($LogRoot) { $LogRoot } else { Join-Path $repoRoot 'Logs\product-regressions' }) -Label 'LogRoot'
if ($Mode -eq 'player') {
    if ([string]::IsNullOrWhiteSpace($BuildPath)) { throw '-BuildPath is required for player mode.' }
    $resolvedBuild = (Resolve-Path -LiteralPath $BuildPath).Path
    if ([IO.Path]::GetExtension($resolvedBuild) -ne '.exe') { throw "Expected a Windows player .exe: $resolvedBuild" }
    if ($Port + $selectedCases.Count - 1 -gt 65535) { throw "Port range $Port..$($Port + $selectedCases.Count - 1) is invalid." }
}
$runId = 'product-{0}-{1:yyyyMMdd-HHmmss-fff}-{2}' -f $Mode, [DateTime]::UtcNow, [guid]::NewGuid().ToString('N').Substring(0, 8)
$runDirectory = Join-Path $resolvedLogRoot $runId
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$profileBase = if ($ProfileRoot) { Resolve-WorktreePath -Path $ProfileRoot -Label 'ProfileRoot' } else { Join-Path $runDirectory 'profiles' }
$runProfileRoot = Join-Path $profileBase $runId
if ($Mode -eq 'player') { New-Item -ItemType Directory -Path $runProfileRoot -Force | Out-Null }

try {
    if ($Mode -eq 'fixture') {
        $fixtureScript = Join-Path $PSScriptRoot 'test-product-regression-log-checks.ps1'
        $fixtureLog = Join-Path $runDirectory 'fixture-parser-checks.log'
        $pwsh = (Get-Command pwsh -ErrorAction Stop).Source
        $fixtureOutput = & $pwsh -NoProfile -ExecutionPolicy Bypass -File $fixtureScript 2>&1
        $fixtureExit = $LASTEXITCODE
        $fixtureText = (@($fixtureOutput | ForEach-Object { [string]$_ }) -join "`n")
        Set-Content -LiteralPath $fixtureLog -Value $fixtureText -Encoding UTF8
        if ($fixtureExit -ne 0) {
            $script:runFailures.Add("Fixture parser checks failed with exit code $fixtureExit; inspect $fixtureLog")
        }
        elseif ($fixtureText -notmatch '(?m)^PARSER_CHECKS=PASS\r?$') {
            $script:runFailures.Add("Fixture parser checks did not report PARSER_CHECKS=PASS; inspect $fixtureLog")
        }
        if ($script:runFailures.Count -eq 0) {
            Write-Output "FIXTURE_PASS matrix=all-process-smoke log=$fixtureLog"
        }
        Write-Output "FIXTURE_SCOPE=all-process-smoke requested=$($selectedCases -join ',')"
    }
    else {
        $index = 0
        foreach ($caseName in $selectedCases) {
            $casePort = $Port + $index
            $index++
            $caseLogDir = Join-Path $runDirectory (Join-Path 'logs' $caseName)
            $caseProfileDir = Join-Path $runProfileRoot $caseName
            New-Item -ItemType Directory -Path $caseLogDir -Force | Out-Null
            Write-Output "CASE_RUN name=$caseName port=$casePort logs=$caseLogDir profiles=$caseProfileDir"
            $result = Invoke-PlayerCase -CaseName $caseName -CasePort $casePort -CaseLogDir $caseLogDir -CaseProfileDir $caseProfileDir
            $check = Test-ProductRegressionCase -Case $caseName -Logs $result.Logs -StorageSentinelUnchanged $result.StorageSentinelUnchanged
            if ($check.Passed) {
                Write-Output "CASE_PASS name=$caseName"
            }
            else {
                foreach ($issue in $check.Issues) {
                    $script:runFailures.Add("$caseName`: $issue")
                }
                Write-Output "CASE_FAIL name=$caseName issues=$($check.Issues.Count)"
            }
        }
    }
}
finally {
    foreach ($record in $script:started) {
        $record.Process.Refresh()
        if (-not $record.Process.HasExited) {
            Stop-CaseProcess -Record $record -Reason 'final-cleanup'
        }
    }

    foreach ($record in $script:started) {
        $record.Process.Refresh()
        if ($record.Process.HasExited) {
            $closure = if ($record.Forced) { 'harness-stop' } else { "natural-exit=$($record.Process.ExitCode)" }
            Write-Output "PROCESS_CLOSED role=$($record.Role) case=$($record.Case) pid=$($record.Process.Id) result=$closure"
            if (-not $record.Forced -and $record.Process.ExitCode -ne 0) {
                $script:runFailures.Add("Natural process exit was $($record.Process.ExitCode) for $($record.Case) pid $($record.Process.Id)")
            }
        }
        else {
            $script:runFailures.Add("Own process $($record.Process.Id) for $($record.Case) remained alive after cleanup")
        }
    }

    if (-not $KeepProfiles -and (Test-Path -LiteralPath $runProfileRoot)) {
        Remove-Item -LiteralPath $runProfileRoot -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $runProfileRoot) {
            $script:runFailures.Add("Could not remove temporary profile directory $runProfileRoot")
        }
        elseif (-not $ProfileRoot -and (Test-Path -LiteralPath $profileBase) -and @(Get-ChildItem -LiteralPath $profileBase).Count -eq 0) {
            Remove-Item -LiteralPath $profileBase -Force -ErrorAction SilentlyContinue
        }
    }
}

$profileOutput = if ($Mode -eq 'player') { $runProfileRoot } else { 'none' }
Write-Output "RUN_MODE=$Mode LOGS=$runDirectory PROFILES=$profileOutput"
if ($script:runFailures.Count -gt 0) {
    foreach ($failure in $script:runFailures) { Write-Output "FAILURE=$failure" }
    Write-Output 'RESULT=FAIL'
    exit 1
}
Write-Output 'RESULT=PASS'
