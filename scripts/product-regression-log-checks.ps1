$script:ProductRoles = @('Solo', 'Host', 'Guest')
$script:ProductFlowStates = @('Home', 'Hut', 'Mission', 'Returning')
$script:ProductFlowResults = @('Started', 'Completed', 'Failed')
$script:ProductSessionStates = @('Idle', 'LoadingHut', 'Hosting', 'Joining', 'HutReady', 'PreparingMission', 'MissionReady', 'Running', 'Returning', 'Failed', 'Stopped')
$script:ProductLaunchResults = @('Ready', 'Failed')
$script:ProductJoinRejectReasons = @('SessionClosed', 'SessionFull', 'SessionNotFound', 'InvalidEndpoint', 'NotReady')
$script:ProductMissionEventTypes = @('GiftDelivered', 'PlayerCaptured', 'PlayerFreed', 'Completed', 'Failed', 'Abandoned')
$script:ProductSaveResults = @('Committed', 'AlreadyCommitted', 'Rejected')
$script:ProductShutdownResults = @('Completed', 'Failed')
$script:ProductErrorCodes = @(
    'InvalidSlot', 'StorageUnavailable', 'InvalidEndpoint', 'BindFailed', 'ConnectionTimeout',
    'SessionNotFound', 'SessionFull', 'SessionClosed', 'NotHost', 'TooFewPlayers', 'NotReady',
    'SceneLoadFailed', 'HostLeft', 'Disconnected', 'AlreadyStarted', 'CommitFailed'
)
$script:ProductMarkerNames = @(
    'PRODUCT_FLOW', 'SESSION_LAUNCH', 'SESSION_STATE', 'SESSION_JOIN_REJECT', 'SESSION_READY',
    'MISSION_START_AUTHORIZED', 'MISSION_EVENT', 'SAVE_COMMIT', 'SESSION_SHUTDOWN'
)

function Add-ProductIssue {
    param(
        [Parameter(Mandatory = $true)] $Issues,
        [Parameter(Mandatory = $true)][string] $Message
    )

    if (-not $Issues.Contains($Message)) { $Issues.Add($Message) }
}

function Test-ProductGuidValue {
    param([AllowEmptyString()][string] $Value)

    $parsed = [guid]::Empty
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    [guid]::TryParse($Value, [ref]$parsed)
}

function Test-ProductErrorOrNone {
    param([AllowEmptyString()][string] $Value)

    if ($Value -in @('None', 'none', '-')) { return $true }
    return ($script:ProductErrorCodes -contains $Value)
}

function Assert-ProductFields {
    param(
        $Record,
        [string[]] $Allowed,
        [string[]] $Required,
        $Issues
    )

    $prefix = "$($Record.Marker) line $($Record.LineNumber)"
    foreach ($field in @($Record.Fields.Keys)) {
        if ($Allowed -notcontains $field) {
            Add-ProductIssue $Issues "$prefix has unexpected field '$field'"
        }
    }
    foreach ($field in $Required) {
        if (-not $Record.Fields.Contains($field)) {
            Add-ProductIssue $Issues "$prefix is missing required field '$field'"
        }
    }
}

function Test-ProductMarkerRecord {
    param($Record, $Issues)

    $prefix = "$($Record.Marker) line $($Record.LineNumber)"
    $fields = $Record.Fields

    switch ($Record.Marker) {
        'PRODUCT_FLOW' {
            Assert-ProductFields $Record @('role', 'state', 'result', 'error') @('role', 'state', 'result') $Issues
            if ($fields.Contains('role') -and $script:ProductRoles -notcontains $fields['role']) {
                Add-ProductIssue $Issues "$prefix has invalid role '$($fields['role'])'"
            }
            if ($fields.Contains('state') -and $script:ProductFlowStates -notcontains $fields['state']) {
                Add-ProductIssue $Issues "$prefix has invalid state '$($fields['state'])'"
            }
            if ($fields.Contains('result') -and $script:ProductFlowResults -notcontains $fields['result']) {
                Add-ProductIssue $Issues "$prefix has invalid result '$($fields['result'])'"
            }
            if ($fields.Contains('result') -and $fields['result'] -eq 'Failed') {
                if (-not $fields.Contains('error') -or [string]::IsNullOrWhiteSpace($fields['error'])) {
                    Add-ProductIssue $Issues "$prefix is Failed without an error code"
                }
                elseif ($script:ProductErrorCodes -notcontains $fields['error']) {
                    Add-ProductIssue $Issues "$prefix has unknown error '$($fields['error'])'"
                }
            }
            if ($fields.Contains('error') -and -not (Test-ProductErrorOrNone $fields['error'])) {
                Add-ProductIssue $Issues "$prefix has unexpected error value '$($fields['error'])'"
            }
            if ($fields.Contains('result') -and $fields['result'] -ne 'Failed' -and $fields.Contains('error') -and
                $script:ProductErrorCodes -contains $fields['error']) {
                Add-ProductIssue $Issues "$prefix reports an error code without Failed"
            }
        }
        'SESSION_LAUNCH' {
            Assert-ProductFields $Record @('role', 'result', 'error') @('role', 'result') $Issues
            if ($fields.Contains('role') -and $script:ProductRoles -notcontains $fields['role']) {
                Add-ProductIssue $Issues "$prefix has invalid role '$($fields['role'])'"
            }
            if ($fields.Contains('result') -and $script:ProductLaunchResults -notcontains $fields['result']) {
                Add-ProductIssue $Issues "$prefix has invalid result '$($fields['result'])'"
            }
            if ($fields.Contains('result') -and $fields['result'] -eq 'Failed') {
                if (-not $fields.Contains('error') -or [string]::IsNullOrWhiteSpace($fields['error'])) {
                    Add-ProductIssue $Issues "$prefix is Failed without an error code"
                }
                elseif ($script:ProductErrorCodes -notcontains $fields['error']) {
                    Add-ProductIssue $Issues "$prefix has unknown error '$($fields['error'])'"
                }
            }
            if ($fields.Contains('result') -and $fields['result'] -eq 'Ready' -and $fields.Contains('error') -and
                $script:ProductErrorCodes -contains $fields['error']) {
                Add-ProductIssue $Issues "$prefix is Ready but carries error '$($fields['error'])'"
            }
        }
        'SESSION_STATE' {
            Assert-ProductFields $Record @('state', 'role') @('state', 'role') $Issues
            if ($fields.Contains('state') -and $script:ProductSessionStates -notcontains $fields['state']) {
                Add-ProductIssue $Issues "$prefix has invalid state '$($fields['state'])'"
            }
            if ($fields.Contains('role') -and $script:ProductRoles -notcontains $fields['role']) {
                Add-ProductIssue $Issues "$prefix has invalid role '$($fields['role'])'"
            }
        }
        'SESSION_JOIN_REJECT' {
            Assert-ProductFields $Record @('reason') @('reason') $Issues
            if ($fields.Contains('reason') -and $script:ProductJoinRejectReasons -notcontains $fields['reason']) {
                Add-ProductIssue $Issues "$prefix has invalid reason '$($fields['reason'])'"
            }
        }
        'SESSION_READY' {
            Assert-ProductFields $Record @('peerId', 'scene', 'roundId') @('peerId', 'scene', 'roundId') $Issues
            if ($fields.Contains('scene') -and $fields['scene'] -ne 'Mission') {
                Add-ProductIssue $Issues "$prefix must report scene=Mission"
            }
            if ($fields.Contains('roundId') -and -not (Test-ProductGuidValue $fields['roundId'])) {
                Add-ProductIssue $Issues "$prefix has an invalid roundId"
            }
            if ($fields.Contains('peerId') -and [string]::IsNullOrWhiteSpace($fields['peerId'])) {
                Add-ProductIssue $Issues "$prefix has an empty peerId"
            }
        }
        'MISSION_START_AUTHORIZED' {
            Assert-ProductFields $Record @('roundId', 'players') @('roundId', 'players') $Issues
            if ($fields.Contains('roundId') -and -not (Test-ProductGuidValue $fields['roundId'])) {
                Add-ProductIssue $Issues "$prefix has an invalid roundId"
            }
            if ($fields.Contains('players')) {
                $players = 0
                if (-not [int]::TryParse($fields['players'], [ref]$players) -or $players -lt 1 -or $players -gt 4) {
                    Add-ProductIssue $Issues "$prefix has invalid player count '$($fields['players'])'"
                }
            }
        }
        'MISSION_EVENT' {
            Assert-ProductFields $Record @('type', 'roundId', 'peerId') @('type', 'roundId', 'peerId') $Issues
            if ($fields.Contains('type') -and $script:ProductMissionEventTypes -notcontains $fields['type']) {
                Add-ProductIssue $Issues "$prefix has invalid type '$($fields['type'])'"
            }
            if ($fields.Contains('roundId') -and -not (Test-ProductGuidValue $fields['roundId'])) {
                Add-ProductIssue $Issues "$prefix has an invalid roundId"
            }
            if ($fields.Contains('peerId') -and [string]::IsNullOrWhiteSpace($fields['peerId'])) {
                Add-ProductIssue $Issues "$prefix has an empty peerId"
            }
        }
        'SAVE_COMMIT' {
            Assert-ProductFields $Record @('role', 'roundId', 'slotId', 'missionId', 'result', 'reason') @('role', 'roundId', 'slotId', 'missionId', 'result') $Issues
            if ($fields.Contains('role') -and @('Solo', 'Host') -notcontains $fields['role']) {
                Add-ProductIssue $Issues "$prefix must never attribute a save to role '$($fields['role'])'"
            }
            if ($fields.Contains('result') -and $script:ProductSaveResults -notcontains $fields['result']) {
                Add-ProductIssue $Issues "$prefix has invalid result '$($fields['result'])'"
            }
            if ($fields.Contains('roundId') -and -not (Test-ProductGuidValue $fields['roundId'])) {
                Add-ProductIssue $Issues "$prefix has an invalid roundId"
            }
            if ($fields.Contains('slotId') -and -not (Test-ProductGuidValue $fields['slotId'])) {
                Add-ProductIssue $Issues "$prefix has an invalid slotId"
            }
            if ($fields.Contains('missionId') -and [string]::IsNullOrWhiteSpace($fields['missionId'])) {
                Add-ProductIssue $Issues "$prefix has an empty missionId"
            }
            if ($fields.Contains('result') -and $fields['result'] -eq 'Rejected' -and
                (-not $fields.Contains('reason') -or [string]::IsNullOrWhiteSpace($fields['reason']))) {
                Add-ProductIssue $Issues "$prefix is Rejected without a reason"
            }
        }
        'SESSION_SHUTDOWN' {
            Assert-ProductFields $Record @('result', 'managerCount', 'reason') @('result', 'managerCount') $Issues
            if ($fields.Contains('result') -and $script:ProductShutdownResults -notcontains $fields['result']) {
                Add-ProductIssue $Issues "$prefix has invalid result '$($fields['result'])'"
            }
            if ($fields.Contains('managerCount') -and @('0', '1') -notcontains $fields['managerCount']) {
                Add-ProductIssue $Issues "$prefix has invalid managerCount '$($fields['managerCount'])'"
            }
            if ($fields.Contains('result') -and $fields['result'] -eq 'Completed' -and $fields.Contains('managerCount') -and
                $fields['managerCount'] -ne '0') {
                Add-ProductIssue $Issues "$prefix completed while a manager remained"
            }
            if ($fields.Contains('result') -and $fields['result'] -eq 'Failed' -and
                (-not $fields.Contains('reason') -or [string]::IsNullOrWhiteSpace($fields['reason']))) {
                Add-ProductIssue $Issues "$prefix is Failed without a reason"
            }
        }
    }
}

function ConvertFrom-ProductMarkerText {
    param(
        [AllowEmptyString()][string] $Text,
        [Parameter(Mandatory = $true)][string] $Marker
    )

    $records = [Collections.Generic.List[object]]::new()
    $issues = [Collections.Generic.List[string]]::new()
    $lines = $Text -split "`r?`n"
    $markerPattern = '(?<![A-Za-z0-9_])' + [regex]::Escape($Marker) + '(?=\s|$)'

    for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
        $line = $lines[$lineIndex]
        $match = [regex]::Match($line, $markerPattern)
        if (-not $match.Success) { continue }

        $rest = $line.Substring($match.Index + $match.Length).Trim()
        $fields = [ordered]@{}
        $valid = $true

        while ($rest.Length -gt 0) {
            $fieldMatch = [regex]::Match($rest, '^(?<name>[A-Za-z][A-Za-z0-9_]*)=(?:"(?<quoted>[^"]*)"|(?<bare>\S+))(?:\s+|$)')
            if (-not $fieldMatch.Success) {
                $issues.Add("$Marker line $($lineIndex + 1) has an invalid field token near '$rest'")
                $valid = $false
                break
            }

            $name = $fieldMatch.Groups['name'].Value
            $value = if ($fieldMatch.Groups['quoted'].Success) { $fieldMatch.Groups['quoted'].Value } else { $fieldMatch.Groups['bare'].Value }
            if ($fields.Contains($name)) {
                $issues.Add("$Marker line $($lineIndex + 1) repeats field '$name'")
                $valid = $false
            }
            else {
                $fields[$name] = $value
            }
            $rest = $rest.Substring($fieldMatch.Length).Trim()
        }

        if ($fields.Count -eq 0) {
            $issues.Add("$Marker line $($lineIndex + 1) has no fields")
            $valid = $false
        }

        $records.Add([pscustomobject]@{
            Marker     = $Marker
            LineNumber = $lineIndex + 1
            Fields     = $fields
            Valid      = $valid
            Raw        = $line
        })
    }

    [pscustomobject]@{ Records = @($records); Issues = @($issues) }
}

function Get-ProductMarkerFirstLine {
    param($Records)

    $lineNumber = $null
    foreach ($record in @($Records)) {
        if ($null -eq $lineNumber -or $record.LineNumber -lt $lineNumber) { $lineNumber = $record.LineNumber }
    }
    $lineNumber
}

function Get-ProductLogAnalysis {
    param(
        [AllowEmptyString()][string] $Text,
        [Parameter(Mandatory = $true)][string] $ExpectedRole
    )

    $issues = [Collections.Generic.List[string]]::new()
    $markers = @{}

    foreach ($marker in $script:ProductMarkerNames) {
        $parsed = ConvertFrom-ProductMarkerText -Text $Text -Marker $marker
        foreach ($issue in $parsed.Issues) { Add-ProductIssue $issues $issue }
        $markers[$marker] = @($parsed.Records)
    }

    foreach ($marker in $script:ProductMarkerNames) {
        foreach ($record in $markers[$marker]) {
            if ($record.Valid) { Test-ProductMarkerRecord $record $issues }
        }
    }

    if ($markers['SESSION_LAUNCH'].Count -gt 1) { Add-ProductIssue $issues 'SESSION_LAUNCH appears more than once in one process log' }
    if ($markers['SESSION_SHUTDOWN'].Count -gt 1) { Add-ProductIssue $issues 'SESSION_SHUTDOWN appears more than once in one process log' }
    if ($markers['MISSION_START_AUTHORIZED'].Count -gt 1) { Add-ProductIssue $issues 'MISSION_START_AUTHORIZED appears more than once in one process log' }

    $terminalByRole = @{}
    foreach ($record in $markers['PRODUCT_FLOW']) {
        if (-not $record.Fields.Contains('role') -or -not $record.Fields.Contains('result')) { continue }
        if (@('Completed', 'Failed') -notcontains $record.Fields['result']) { continue }
        $role = $record.Fields['role']
        if ($terminalByRole.ContainsKey($role)) {
            Add-ProductIssue $issues "PRODUCT_FLOW has more than one terminal event for role $role"
        }
        else {
            $terminalByRole[$role] = $record
        }
    }

    $missionTerminalByRound = @{}
    foreach ($record in $markers['MISSION_EVENT']) {
        if (-not $record.Fields.Contains('roundId') -or -not $record.Fields.Contains('type')) { continue }
        if (@('Completed', 'Failed', 'Abandoned') -notcontains $record.Fields['type']) { continue }
        $roundId = $record.Fields['roundId'].ToLowerInvariant()
        if ($missionTerminalByRound.ContainsKey($roundId)) {
            Add-ProductIssue $issues "MISSION_EVENT has more than one terminal event for round $roundId"
        }
        else {
            $missionTerminalByRound[$roundId] = $record
        }
    }

    $readyKeys = @{}
    foreach ($record in $markers['SESSION_READY']) {
        if (-not $record.Fields.Contains('peerId') -or -not $record.Fields.Contains('roundId')) { continue }
        $key = $record.Fields['peerId'] + '|' + $record.Fields['roundId'].ToLowerInvariant()
        if ($readyKeys.ContainsKey($key)) {
            Add-ProductIssue $issues "SESSION_READY repeats peer $($record.Fields['peerId']) for one round"
        }
        else {
            $readyKeys[$key] = $record
        }
    }

    $saveKeys = @{}
    foreach ($record in $markers['SAVE_COMMIT']) {
        if (-not $record.Fields.Contains('roundId') -or -not $record.Fields.Contains('slotId') -or
            -not $record.Fields.Contains('missionId') -or -not $record.Fields.Contains('result')) { continue }
        $key = (@('role', 'roundId', 'slotId', 'missionId', 'result') | ForEach-Object {
                if ($record.Fields.Contains($_)) { [string]$record.Fields[$_] } else { '' }
            }) -join '|'
        if ($saveKeys.ContainsKey($key)) {
            Add-ProductIssue $issues 'SAVE_COMMIT repeats an identical event'
        }
        else {
            $saveKeys[$key] = $record
        }
    }

    foreach ($record in $markers['SESSION_LAUNCH']) {
        if ($record.Fields.Contains('role') -and $record.Fields['role'] -ne $ExpectedRole) {
            Add-ProductIssue $issues "SESSION_LAUNCH role $($record.Fields['role']) does not match expected role $ExpectedRole"
        }
    }
    foreach ($record in $markers['PRODUCT_FLOW']) {
        if ($record.Fields.Contains('role') -and $record.Fields['role'] -ne $ExpectedRole) {
            Add-ProductIssue $issues "PRODUCT_FLOW role $($record.Fields['role']) does not match expected role $ExpectedRole"
        }
    }
    foreach ($record in $markers['SESSION_STATE']) {
        if ($record.Fields.Contains('role') -and $record.Fields['role'] -ne $ExpectedRole) {
            Add-ProductIssue $issues "SESSION_STATE role $($record.Fields['role']) does not match expected role $ExpectedRole"
        }
    }
    foreach ($record in $markers['SAVE_COMMIT']) {
        if ($record.Fields.Contains('role') -and $record.Fields['role'] -ne $ExpectedRole) {
            Add-ProductIssue $issues "SAVE_COMMIT role $($record.Fields['role']) is not allowed in the $ExpectedRole log"
        }
    }

    if ($ExpectedRole -eq 'Guest') {
        if ($markers['SAVE_COMMIT'].Count -gt 0) { Add-ProductIssue $issues 'a guest log contains SAVE_COMMIT' }
        if ($markers['MISSION_START_AUTHORIZED'].Count -gt 0) { Add-ProductIssue $issues 'a guest log contains MISSION_START_AUTHORIZED' }
    }
    if ($ExpectedRole -eq 'Solo') {
        if ($markers['SESSION_READY'].Count -gt 0) { Add-ProductIssue $issues 'a solo log contains network readiness' }
        if ($markers['MISSION_START_AUTHORIZED'].Count -gt 0) { Add-ProductIssue $issues 'a solo log contains MISSION_START_AUTHORIZED' }
        if ($markers['SESSION_JOIN_REJECT'].Count -gt 0) { Add-ProductIssue $issues 'a solo log contains a join rejection' }
    }

    $launchLine = Get-ProductMarkerFirstLine $markers['SESSION_LAUNCH']
    $readyLine = Get-ProductMarkerFirstLine $markers['SESSION_READY']
    $authorizedLine = Get-ProductMarkerFirstLine $markers['MISSION_START_AUTHORIZED']
    $completedLine = $null
    foreach ($record in $markers['MISSION_EVENT']) {
        if ($record.Fields.Contains('type') -and $record.Fields['type'] -eq 'Completed') {
            if ($null -eq $completedLine -or $record.LineNumber -lt $completedLine) { $completedLine = $record.LineNumber }
        }
    }
    $saveLine = Get-ProductMarkerFirstLine $markers['SAVE_COMMIT']
    $shutdownLine = Get-ProductMarkerFirstLine $markers['SESSION_SHUTDOWN']
    $flowTerminalLine = Get-ProductMarkerFirstLine @($markers['PRODUCT_FLOW'] | Where-Object {
            $_.Fields.Contains('result') -and @('Completed', 'Failed') -contains $_.Fields['result']
        })

    if ($null -ne $launchLine -and $null -ne $readyLine -and $launchLine -gt $readyLine) {
        Add-ProductIssue $issues 'SESSION_READY appears before SESSION_LAUNCH'
    }
    if ($null -ne $readyLine -and $null -ne $authorizedLine -and $readyLine -gt $authorizedLine) {
        Add-ProductIssue $issues 'MISSION_START_AUTHORIZED appears before SESSION_READY'
    }
    if ($null -ne $completedLine -and $null -ne $saveLine -and $completedLine -gt $saveLine) {
        Add-ProductIssue $issues 'SAVE_COMMIT appears before the completed mission event'
    }
    if ($null -ne $authorizedLine -and $null -ne $shutdownLine -and $authorizedLine -gt $shutdownLine) {
        Add-ProductIssue $issues 'SESSION_SHUTDOWN appears before MISSION_START_AUTHORIZED'
    }
    if ($null -ne $launchLine -and $null -ne $shutdownLine -and $launchLine -gt $shutdownLine) {
        Add-ProductIssue $issues 'SESSION_SHUTDOWN appears before SESSION_LAUNCH'
    }
    if ($null -ne $flowTerminalLine -and $null -ne $shutdownLine -and $flowTerminalLine -gt $shutdownLine) {
        Add-ProductIssue $issues 'SESSION_SHUTDOWN appears before the terminal PRODUCT_FLOW event'
    }

    [pscustomobject]@{
        ExpectedRole = $ExpectedRole
        Markers      = $markers
        Issues       = @($issues)
    }
}

function New-ProductCheckResult {
    param($Issues)

    $items = @($Issues | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
    [pscustomobject]@{ Passed = ($items.Count -eq 0); Issues = $items }
}

function ConvertTo-ProductRoleLogs {
    param(
        [Parameter(Mandatory = $true)][Collections.IDictionary] $Logs,
        [Parameter(Mandatory = $true)][Collections.IDictionary] $ExpectedRoles
    )

    $issues = [Collections.Generic.List[string]]::new()
    $roleLogs = [Collections.Generic.List[object]]::new()
    foreach ($name in $ExpectedRoles.Keys) {
        if (-not $Logs.Contains($name)) {
            Add-ProductIssue $issues "missing log for role '$name'"
            continue
        }
        $text = [string]$Logs[$name]
        if ([string]::IsNullOrWhiteSpace($text)) {
            Add-ProductIssue $issues "log for role '$name' is empty"
        }
        $roleLogs.Add([pscustomobject]@{ Name = $name; Role = $ExpectedRoles[$name]; Text = $text })
    }

    [pscustomobject]@{ RoleLogs = @($roleLogs); Issues = @($issues) }
}

function Get-ProductSoloLogCheck {
    param(
        [Parameter(Mandatory = $true)][object] $RoleLog,
        [string[]] $AllowedCommitResults = @('Committed')
    )

    $model = Get-ProductLogAnalysis -Text $RoleLog.Text -ExpectedRole 'Solo'
    $issues = [Collections.Generic.List[string]]::new()
    foreach ($issue in $model.Issues) { Add-ProductIssue $issues $issue }

    $launch = @($model.Markers['SESSION_LAUNCH'])
    if ($launch.Count -ne 1) { Add-ProductIssue $issues 'exactly one solo SESSION_LAUNCH is required' }
    elseif ($launch[0].Fields['result'] -ne 'Ready') { Add-ProductIssue $issues 'the solo launch did not become Ready' }

    $flowTerminals = @($model.Markers['PRODUCT_FLOW'] | Where-Object {
            $_.Fields.Contains('result') -and @('Completed', 'Failed') -contains $_.Fields['result']
        })
    if ($flowTerminals.Count -ne 1) { Add-ProductIssue $issues 'exactly one terminal solo PRODUCT_FLOW is required' }
    elseif ($flowTerminals[0].Fields['result'] -ne 'Completed' -or $flowTerminals[0].Fields['state'] -ne 'Home') {
        Add-ProductIssue $issues 'the solo PRODUCT_FLOW did not complete at Home'
    }

    $missionCompleted = @($model.Markers['MISSION_EVENT'] | Where-Object {
            $_.Fields.Contains('type') -and $_.Fields['type'] -eq 'Completed'
        })
    if ($missionCompleted.Count -ne 1) { Add-ProductIssue $issues 'exactly one solo MISSION_EVENT type=Completed is required' }
    $missionFailures = @($model.Markers['MISSION_EVENT'] | Where-Object {
            $_.Fields.Contains('type') -and @('Failed', 'Abandoned') -contains $_.Fields['type']
        })
    if ($missionFailures.Count -gt 0) { Add-ProductIssue $issues 'the solo mission reported a failed or abandoned event' }

    $save = @($model.Markers['SAVE_COMMIT'])
    if ($save.Count -ne 1) { Add-ProductIssue $issues 'exactly one solo SAVE_COMMIT is required' }
    else {
        if ($AllowedCommitResults -notcontains $save[0].Fields['result']) {
            Add-ProductIssue $issues "the solo SAVE_COMMIT result '$($save[0].Fields['result'])' is not accepted for this case"
        }
        if ($save[0].Fields['role'] -ne 'Solo') { Add-ProductIssue $issues 'the solo save was not attributed to role Solo' }
        if ($missionCompleted.Count -eq 1 -and $save[0].Fields['roundId'] -ne $missionCompleted[0].Fields['roundId']) {
            Add-ProductIssue $issues 'the solo save round does not match the completed mission round'
        }
    }

    $shutdown = @($model.Markers['SESSION_SHUTDOWN'])
    if ($shutdown.Count -ne 1) { Add-ProductIssue $issues 'exactly one solo SESSION_SHUTDOWN is required' }
    elseif ($shutdown[0].Fields['result'] -ne 'Completed' -or $shutdown[0].Fields['managerCount'] -ne '0') {
        Add-ProductIssue $issues 'the solo shutdown did not complete with managerCount=0'
    }

    [pscustomobject]@{
        Passed           = ($issues.Count -eq 0)
        Issues           = @($issues)
        Commit           = if ($save.Count -eq 1) { $save[0] } else { $null }
        MissionCompleted = if ($missionCompleted.Count -eq 1) { $missionCompleted[0] } else { $null }
    }
}

function Test-ProductSoloTransitionLog {
    param([AllowEmptyString()][string] $LogText)

    $roleLog = [pscustomobject]@{ Name = 'solo'; Role = 'Solo'; Text = $LogText }
    $result = Get-ProductSoloLogCheck -RoleLog $roleLog -AllowedCommitResults @('Committed')
    New-ProductCheckResult $result.Issues
}

function Test-ProductNetworkTransitionLogs {
    param(
        [Parameter(Mandatory = $true)][object[]] $RoleLogs,
        [Parameter(Mandatory = $true)][int] $ExpectedPlayers
    )

    $issues = [Collections.Generic.List[string]]::new()
    $models = [Collections.Generic.List[object]]::new()
    foreach ($roleLog in $RoleLogs) {
        $model = Get-ProductLogAnalysis -Text $roleLog.Text -ExpectedRole $roleLog.Role
        foreach ($issue in $model.Issues) { Add-ProductIssue $issues $issue }
        $models.Add([pscustomobject]@{ Name = $roleLog.Name; Role = $roleLog.Role; Model = $model })
    }

    foreach ($entry in $models) {
        $launch = @($entry.Model.Markers['SESSION_LAUNCH'])
        if ($launch.Count -ne 1) { Add-ProductIssue $issues "$($entry.Name) has no unique SESSION_LAUNCH" }
        elseif ($launch[0].Fields['result'] -ne 'Ready') { Add-ProductIssue $issues "$($entry.Name) launch did not become Ready" }
    }

    $ready = [Collections.Generic.List[object]]::new()
    foreach ($entry in $models) {
        foreach ($record in $entry.Model.Markers['SESSION_READY']) { $ready.Add($record) }
    }
    $readyPeers = @{}
    $readyRounds = @{}
    foreach ($record in $ready) {
        $readyPeers[$record.Fields['peerId']] = $true
        $readyRounds[$record.Fields['roundId'].ToLowerInvariant()] = $true
    }
    if ($readyPeers.Count -ne $ExpectedPlayers) {
        Add-ProductIssue $issues "expected $ExpectedPlayers distinct ready peers but found $($readyPeers.Count)"
    }
    if ($readyRounds.Count -ne 1) {
        Add-ProductIssue $issues 'ready peers do not share exactly one roundId'
    }
    $roundId = if ($readyRounds.Count -eq 1) { @($readyRounds.Keys)[0] } else { $null }

    $authorized = [Collections.Generic.List[object]]::new()
    foreach ($entry in $models) {
        foreach ($record in $entry.Model.Markers['MISSION_START_AUTHORIZED']) { $authorized.Add($record) }
    }
    if ($authorized.Count -ne 1) {
        Add-ProductIssue $issues 'exactly one MISSION_START_AUTHORIZED is required across the session'
    }
    else {
        if ($authorized[0].Fields['players'] -ne [string]$ExpectedPlayers) {
            Add-ProductIssue $issues "MISSION_START_AUTHORIZED players=$($authorized[0].Fields['players']) does not match $ExpectedPlayers"
        }
        if ($null -ne $roundId -and $authorized[0].Fields['roundId'].ToLowerInvariant() -ne $roundId) {
            Add-ProductIssue $issues 'MISSION_START_AUTHORIZED round does not match the ready round'
        }
    }

    foreach ($entry in $models) {
        $flowTerminals = @($entry.Model.Markers['PRODUCT_FLOW'] | Where-Object {
                $_.Fields.Contains('result') -and @('Completed', 'Failed') -contains $_.Fields['result']
            })
        if ($flowTerminals.Count -ne 1) { Add-ProductIssue $issues "$($entry.Name) has no unique terminal PRODUCT_FLOW" }
        elseif ($flowTerminals[0].Fields['result'] -ne 'Completed' -or $flowTerminals[0].Fields['state'] -ne 'Home') {
            Add-ProductIssue $issues "$($entry.Name) did not complete at Home"
        }

        $shutdown = @($entry.Model.Markers['SESSION_SHUTDOWN'])
        if ($shutdown.Count -ne 1) { Add-ProductIssue $issues "$($entry.Name) has no unique SESSION_SHUTDOWN" }
        elseif ($shutdown[0].Fields['result'] -ne 'Completed' -or $shutdown[0].Fields['managerCount'] -ne '0') {
            Add-ProductIssue $issues "$($entry.Name) shutdown did not complete with managerCount=0"
        }

        if ($entry.Model.Markers['SESSION_JOIN_REJECT'].Count -gt 0) {
            Add-ProductIssue $issues "$($entry.Name) contains an unexpected join rejection"
        }
    }

    $hostEntry = @($models | Where-Object { $_.Role -eq 'Host' })
    if ($hostEntry.Count -ne 1) {
        Add-ProductIssue $issues 'exactly one host log is required'
    }
    else {
        $hostCompleted = @($hostEntry[0].Model.Markers['MISSION_EVENT'] | Where-Object {
                $_.Fields.Contains('type') -and $_.Fields['type'] -eq 'Completed'
            })
        if ($hostCompleted.Count -ne 1) { Add-ProductIssue $issues 'the host must log exactly one MISSION_EVENT type=Completed' }
        elseif ($null -ne $roundId -and $hostCompleted[0].Fields['roundId'].ToLowerInvariant() -ne $roundId) {
            Add-ProductIssue $issues 'the host completion round does not match the ready round'
        }
    }

    $saves = [Collections.Generic.List[object]]::new()
    foreach ($entry in $models) {
        foreach ($record in $entry.Model.Markers['SAVE_COMMIT']) { $saves.Add($record) }
    }
    if ($saves.Count -ne 1) {
        Add-ProductIssue $issues 'exactly one host SAVE_COMMIT is required across the session'
    }
    else {
        if ($saves[0].Fields['role'] -ne 'Host') { Add-ProductIssue $issues 'the network save was not attributed to the host' }
        if ($saves[0].Fields['result'] -ne 'Committed') { Add-ProductIssue $issues 'the network save was not Committed' }
        if ($null -ne $roundId -and $saves[0].Fields['roundId'].ToLowerInvariant() -ne $roundId) {
            Add-ProductIssue $issues 'the save round does not match the ready round'
        }
    }

    $missionFailures = [Collections.Generic.List[object]]::new()
    foreach ($entry in $models) {
        foreach ($record in $entry.Model.Markers['MISSION_EVENT']) {
            if ($record.Fields.Contains('type') -and @('Failed', 'Abandoned') -contains $record.Fields['type']) {
                $missionFailures.Add($record)
            }
        }
    }
    if ($missionFailures.Count -gt 0) { Add-ProductIssue $issues 'the completed network mission reported a failed or abandoned event' }

    [pscustomobject]@{ Passed = ($issues.Count -eq 0); Issues = @($issues) }
}

function Test-ProductSaveRoundtripLogs {
    param([Parameter(Mandatory = $true)][object[]] $RoleLogs)

    if ($RoleLogs.Count -ne 2) {
        return New-ProductCheckResult @('exactly two sequential solo runs are required for save-roundtrip')
    }

    $first = Get-ProductSoloLogCheck -RoleLog $RoleLogs[0] -AllowedCommitResults @('Committed')
    $second = Get-ProductSoloLogCheck -RoleLog $RoleLogs[1] -AllowedCommitResults @('Committed', 'AlreadyCommitted')
    $issues = [Collections.Generic.List[string]]::new()
    foreach ($issue in $first.Issues) { Add-ProductIssue $issues $issue }
    foreach ($issue in $second.Issues) { Add-ProductIssue $issues $issue }

    if ($null -ne $first.Commit -and $null -ne $second.Commit) {
        if ($first.Commit.Fields['slotId'] -ne $second.Commit.Fields['slotId']) {
            Add-ProductIssue $issues 'the rounded slotId changed between the two runs'
        }
        if ($first.Commit.Fields['missionId'] -ne $second.Commit.Fields['missionId']) {
            Add-ProductIssue $issues 'the completed missionId changed between the two runs'
        }
        if ($first.Commit.Fields['roundId'] -eq $second.Commit.Fields['roundId']) {
            Add-ProductIssue $issues 'the second run reused the first roundId instead of starting a new round'
        }
        if ($second.Commit.Fields['result'] -eq 'Rejected') {
            Add-ProductIssue $issues 'the reloaded save rejected the second completion'
        }
    }

    New-ProductCheckResult $issues
}

function Test-ProductNoClientWriteLogs {
    param(
        [Parameter(Mandatory = $true)][object[]] $RoleLogs,
        [bool] $StorageSentinelUnchanged = $true
    )

    $network = Test-ProductNetworkTransitionLogs -RoleLogs $RoleLogs -ExpectedPlayers 2
    $issues = [Collections.Generic.List[string]]::new()
    foreach ($issue in $network.Issues) { Add-ProductIssue $issues $issue }

    foreach ($roleLog in $RoleLogs) {
        if ($roleLog.Role -eq 'Guest') {
            $model = Get-ProductLogAnalysis -Text $roleLog.Text -ExpectedRole 'Guest'
            if ($model.Markers['SAVE_COMMIT'].Count -gt 0) {
                Add-ProductIssue $issues "$($roleLog.Name) emitted SAVE_COMMIT"
            }
        }
    }
    if (-not $StorageSentinelUnchanged) {
        Add-ProductIssue $issues 'the shared storage sentinel changed while the guest was connected'
    }

    New-ProductCheckResult $issues
}

function Test-ProductInvalidJoinLogs {
    param([Parameter(Mandatory = $true)][object[]] $RoleLogs)

    if ($RoleLogs.Count -ne 1) {
        return New-ProductCheckResult @('invalid-join requires exactly one guest log')
    }

    $model = Get-ProductLogAnalysis -Text $RoleLogs[0].Text -ExpectedRole 'Guest'
    $issues = [Collections.Generic.List[string]]::new()
    foreach ($issue in $model.Issues) { Add-ProductIssue $issues $issue }

    $launch = @($model.Markers['SESSION_LAUNCH'])
    if ($launch.Count -ne 1) { Add-ProductIssue $issues 'exactly one invalid SESSION_LAUNCH is required' }
    elseif ($launch[0].Fields['result'] -ne 'Failed' -or $launch[0].Fields['error'] -ne 'InvalidEndpoint') {
        Add-ProductIssue $issues 'the invalid join did not fail with InvalidEndpoint'
    }

    $reject = @($model.Markers['SESSION_JOIN_REJECT'])
    if ($reject.Count -ne 1) { Add-ProductIssue $issues 'exactly one invalid SESSION_JOIN_REJECT is required' }
    elseif ($reject[0].Fields['reason'] -ne 'InvalidEndpoint') {
        Add-ProductIssue $issues 'the invalid join rejection did not use reason InvalidEndpoint'
    }

    if ($model.Markers['SESSION_READY'].Count -gt 0) { Add-ProductIssue $issues 'the invalid join reached network readiness' }
    if ($model.Markers['MISSION_START_AUTHORIZED'].Count -gt 0) { Add-ProductIssue $issues 'the invalid join authorized a mission' }
    if ($model.Markers['SAVE_COMMIT'].Count -gt 0) { Add-ProductIssue $issues 'the invalid join wrote a save' }

    $flowTerminals = @($model.Markers['PRODUCT_FLOW'] | Where-Object {
            $_.Fields.Contains('result') -and @('Completed', 'Failed') -contains $_.Fields['result']
        })
    foreach ($record in $flowTerminals) {
        if ($record.Fields['result'] -eq 'Completed') { Add-ProductIssue $issues 'the invalid join completed a product flow' }
        if ($record.Fields['result'] -eq 'Failed' -and $record.Fields['error'] -ne 'InvalidEndpoint') {
            Add-ProductIssue $issues 'the failed join flow did not report InvalidEndpoint'
        }
    }

    $missionCompleted = @($model.Markers['MISSION_EVENT'] | Where-Object {
            $_.Fields.Contains('type') -and $_.Fields['type'] -eq 'Completed'
        })
    if ($missionCompleted.Count -gt 0) { Add-ProductIssue $issues 'the invalid join completed a mission' }

    $shutdown = @($model.Markers['SESSION_SHUTDOWN'])
    if ($shutdown.Count -ne 1) { Add-ProductIssue $issues 'exactly one invalid-join SESSION_SHUTDOWN is required' }
    elseif ($shutdown[0].Fields['result'] -ne 'Completed' -or $shutdown[0].Fields['managerCount'] -ne '0') {
        Add-ProductIssue $issues 'the invalid join did not shut down cleanly'
    }

    New-ProductCheckResult $issues
}

function Test-ProductRejoinLogs {
    param([Parameter(Mandatory = $true)][object[]] $RoleLogs)

    $issues = [Collections.Generic.List[string]]::new()
    foreach ($roleLog in $RoleLogs) {
        $model = Get-ProductLogAnalysis -Text $roleLog.Text -ExpectedRole $roleLog.Role
        foreach ($issue in $model.Issues) { Add-ProductIssue $issues $issue }

        $launch = @($model.Markers['SESSION_LAUNCH'])
        if ($launch.Count -ne 1) { Add-ProductIssue $issues "$($roleLog.Name) has no unique SESSION_LAUNCH" }
        elseif ($launch[0].Fields['result'] -ne 'Ready') { Add-ProductIssue $issues "$($roleLog.Name) launch did not become Ready" }

        if ($model.Markers['MISSION_START_AUTHORIZED'].Count -gt 0) {
            Add-ProductIssue $issues "$($roleLog.Name) authorized a mission during rejoin"
        }
        if ($model.Markers['SAVE_COMMIT'].Count -gt 0) { Add-ProductIssue $issues "$($roleLog.Name) wrote a save during rejoin" }

        $completedFlow = @($model.Markers['PRODUCT_FLOW'] | Where-Object {
                $_.Fields.Contains('result') -and $_.Fields['result'] -eq 'Completed'
            })
        if ($completedFlow.Count -gt 0) { Add-ProductIssue $issues "$($roleLog.Name) completed a product flow during rejoin" }

        if ($model.Markers['SESSION_JOIN_REJECT'].Count -gt 0) {
            Add-ProductIssue $issues "$($roleLog.Name) contains an unexpected join rejection during rejoin"
        }

        $missionCompleted = @($model.Markers['MISSION_EVENT'] | Where-Object {
                $_.Fields.Contains('type') -and $_.Fields['type'] -eq 'Completed'
            })
        if ($missionCompleted.Count -gt 0) { Add-ProductIssue $issues "$($roleLog.Name) completed a mission during rejoin" }
    }

    New-ProductCheckResult $issues
}

function Test-ProductFullHutLogs {
    param([Parameter(Mandatory = $true)][object[]] $RoleLogs)

    if ($RoleLogs.Count -ne 5) {
        return New-ProductCheckResult @('full-hut requires one host, three accepted guests and one rejected guest')
    }

    $issues = [Collections.Generic.List[string]]::new()
    $models = @{}
    foreach ($roleLog in $RoleLogs) {
        $model = Get-ProductLogAnalysis -Text $roleLog.Text -ExpectedRole $roleLog.Role
        foreach ($issue in $model.Issues) { Add-ProductIssue $issues $issue }
        $models[$roleLog.Name] = $model
    }

    foreach ($name in @('host', 'guest1', 'guest2', 'guest3')) {
        if (-not $models.ContainsKey($name)) { Add-ProductIssue $issues "missing full-hut role '$name'"; continue }
        $launch = @($models[$name].Markers['SESSION_LAUNCH'])
        if ($launch.Count -ne 1) { Add-ProductIssue $issues "$name has no unique SESSION_LAUNCH" }
        elseif ($launch[0].Fields['result'] -ne 'Ready') { Add-ProductIssue $issues "$name did not become Ready" }
    }

    if (-not $models.ContainsKey('guest4')) {
        Add-ProductIssue $issues "missing full-hut role 'guest4'"
    }
    else {
        $rejectedLaunch = @($models['guest4'].Markers['SESSION_LAUNCH'])
        if ($rejectedLaunch.Count -ne 1) { Add-ProductIssue $issues 'guest4 has no unique SESSION_LAUNCH' }
        elseif ($rejectedLaunch[0].Fields['result'] -ne 'Failed' -or $rejectedLaunch[0].Fields['error'] -ne 'SessionFull') {
            Add-ProductIssue $issues 'guest4 was not rejected with SessionFull'
        }
    }

    $rejects = [Collections.Generic.List[object]]::new()
    $saves = [Collections.Generic.List[object]]::new()
    $authorized = [Collections.Generic.List[object]]::new()
    $completed = [Collections.Generic.List[object]]::new()
    foreach ($model in $models.Values) {
        foreach ($record in $model.Markers['SESSION_JOIN_REJECT']) { $rejects.Add($record) }
        foreach ($record in $model.Markers['SAVE_COMMIT']) { $saves.Add($record) }
        foreach ($record in $model.Markers['MISSION_START_AUTHORIZED']) { $authorized.Add($record) }
        foreach ($record in $model.Markers['MISSION_EVENT']) {
            if ($record.Fields.Contains('type') -and $record.Fields['type'] -eq 'Completed') { $completed.Add($record) }
        }
    }

    if ($rejects.Count -eq 0) { Add-ProductIssue $issues 'no SessionFull join rejection was logged' }
    foreach ($reject in $rejects) {
        if ($reject.Fields['reason'] -ne 'SessionFull') {
            Add-ProductIssue $issues "full-hut logged an unexpected join reason $($reject.Fields['reason'])"
        }
    }
    if ($authorized.Count -gt 0) { Add-ProductIssue $issues 'full-hut authorized a mission' }
    if ($saves.Count -gt 0) { Add-ProductIssue $issues 'full-hut wrote a save' }
    if ($completed.Count -gt 0) { Add-ProductIssue $issues 'full-hut completed a mission' }

    New-ProductCheckResult $issues
}

function Test-ProductHostLossLogs {
    param([Parameter(Mandatory = $true)][object[]] $RoleLogs)

    if ($RoleLogs.Count -ne 2) {
        return New-ProductCheckResult @('host-loss requires one host and one guest log')
    }

    $issues = [Collections.Generic.List[string]]::new()
    $models = @{}
    foreach ($roleLog in $RoleLogs) {
        $model = Get-ProductLogAnalysis -Text $roleLog.Text -ExpectedRole $roleLog.Role
        foreach ($issue in $model.Issues) { Add-ProductIssue $issues $issue }
        $models[$roleLog.Name] = $model

        $launch = @($model.Markers['SESSION_LAUNCH'])
        if ($launch.Count -ne 1) { Add-ProductIssue $issues "$($roleLog.Name) has no unique SESSION_LAUNCH" }
        elseif ($launch[0].Fields['result'] -ne 'Ready') { Add-ProductIssue $issues "$($roleLog.Name) launch did not become Ready" }
    }

    if (-not $models.ContainsKey('guest')) {
        Add-ProductIssue $issues "missing host-loss role 'guest'"
    }
    else {
        $guest = $models['guest']
        $flowTerminals = @($guest.Markers['PRODUCT_FLOW'] | Where-Object {
                $_.Fields.Contains('result') -and @('Completed', 'Failed') -contains $_.Fields['result']
            })
        if ($flowTerminals.Count -ne 1) { Add-ProductIssue $issues 'the guest has no unique terminal PRODUCT_FLOW after host loss' }
        elseif ($flowTerminals[0].Fields['result'] -ne 'Failed' -or $flowTerminals[0].Fields['error'] -ne 'HostLeft' -or
            $flowTerminals[0].Fields['state'] -ne 'Home') {
            Add-ProductIssue $issues 'the guest did not fail at Home with HostLeft'
        }

        $shutdown = @($guest.Markers['SESSION_SHUTDOWN'])
        if ($shutdown.Count -ne 1) { Add-ProductIssue $issues 'the guest has no unique SESSION_SHUTDOWN after host loss' }
        elseif ($shutdown[0].Fields['result'] -ne 'Completed' -or $shutdown[0].Fields['managerCount'] -ne '0') {
            Add-ProductIssue $issues 'the guest did not shut down cleanly after host loss'
        }

        if ($guest.Markers['SAVE_COMMIT'].Count -gt 0) { Add-ProductIssue $issues 'the guest wrote a save after host loss' }
        if ($guest.Markers['MISSION_START_AUTHORIZED'].Count -gt 0) { Add-ProductIssue $issues 'the guest authorized a mission after host loss' }
        if ($guest.Markers['SESSION_JOIN_REJECT'].Count -gt 0) { Add-ProductIssue $issues 'the guest contains a join rejection after host loss' }

        $missionCompleted = @($guest.Markers['MISSION_EVENT'] | Where-Object {
                $_.Fields.Contains('type') -and $_.Fields['type'] -eq 'Completed'
            })
        if ($missionCompleted.Count -gt 0) { Add-ProductIssue $issues 'the guest completed a mission after host loss' }
    }

    New-ProductCheckResult $issues
}

function Test-ProductRegressionCase {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('solo-transition', 'duo-transition', 'four-transition', 'save-roundtrip', 'no-client-write', 'invalid-join', 'rejoin', 'full-hut', 'host-loss')]
        [string] $Case,
        [Parameter(Mandatory = $true)][Collections.IDictionary] $Logs,
        [bool] $StorageSentinelUnchanged = $true
    )

    $expectedRoles = switch ($Case) {
        'solo-transition' { [ordered]@{ solo = 'Solo' } }
        'duo-transition' { [ordered]@{ host = 'Host'; guest = 'Guest' } }
        'four-transition' { [ordered]@{ host = 'Host'; guest1 = 'Guest'; guest2 = 'Guest'; guest3 = 'Guest' } }
        'save-roundtrip' { [ordered]@{ 'solo-run1' = 'Solo'; 'solo-run2' = 'Solo' } }
        'no-client-write' { [ordered]@{ host = 'Host'; guest = 'Guest' } }
        'invalid-join' { [ordered]@{ guest = 'Guest' } }
        'rejoin' { [ordered]@{ host = 'Host'; guest1 = 'Guest'; guest2 = 'Guest' } }
        'full-hut' { [ordered]@{ host = 'Host'; guest1 = 'Guest'; guest2 = 'Guest'; guest3 = 'Guest'; guest4 = 'Guest' } }
        'host-loss' { [ordered]@{ host = 'Host'; guest = 'Guest' } }
    }

    $converted = ConvertTo-ProductRoleLogs -Logs $Logs -ExpectedRoles $expectedRoles
    if ($converted.Issues.Count -gt 0) {
        return New-ProductCheckResult $converted.Issues
    }

    switch ($Case) {
        'solo-transition' { return Test-ProductSoloTransitionLog -LogText $converted.RoleLogs[0].Text }
        'duo-transition' { return Test-ProductNetworkTransitionLogs -RoleLogs $converted.RoleLogs -ExpectedPlayers 2 }
        'four-transition' { return Test-ProductNetworkTransitionLogs -RoleLogs $converted.RoleLogs -ExpectedPlayers 4 }
        'save-roundtrip' { return Test-ProductSaveRoundtripLogs -RoleLogs $converted.RoleLogs }
        'no-client-write' { return Test-ProductNoClientWriteLogs -RoleLogs $converted.RoleLogs -StorageSentinelUnchanged $StorageSentinelUnchanged }
        'invalid-join' { return Test-ProductInvalidJoinLogs -RoleLogs $converted.RoleLogs }
        'rejoin' { return Test-ProductRejoinLogs -RoleLogs $converted.RoleLogs }
        'full-hut' { return Test-ProductFullHutLogs -RoleLogs $converted.RoleLogs }
        'host-loss' { return Test-ProductHostLossLogs -RoleLogs $converted.RoleLogs }
    }
}
