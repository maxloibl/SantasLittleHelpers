$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'product-regression-cases.ps1')
. (Join-Path $PSScriptRoot 'product-regression-log-checks.ps1')

$roundA = '11111111-1111-4111-8111-111111111111'
$roundB = '22222222-2222-4222-8222-222222222222'
$slotId = '33333333-3333-4333-8333-333333333333'
$missionId = 'delivery-light'

function Assert-Check {
    param([string] $Name, [bool] $Condition)

    if (-not $Condition) { throw "Product parser check failed: $Name" }
    Write-Output "CHECK_PASS $Name"
}

function Assert-ProductCase {
    param(
        [string] $Name,
        [string] $Case,
        [Collections.IDictionary] $Logs,
        [bool] $ExpectPass = $true,
        [bool] $SentinelUnchanged = $true
    )

    $result = Test-ProductRegressionCase -Case $Case -Logs $Logs -StorageSentinelUnchanged $SentinelUnchanged
    if ($result.Passed -ne $ExpectPass) {
        $detail = if ($result.Issues.Count -gt 0) { $result.Issues -join '; ' } else { 'no issues reported' }
        throw "Product case check failed: $Name (expected pass=$ExpectPass, got pass=$($result.Passed)): $detail"
    }
    Write-Output "CHECK_PASS $Name"
}

function New-SoloFixture {
    param(
        [string] $RoundId = $roundA,
        [string] $Slot = $slotId,
        [string] $Mission = $missionId,
        [string] $CommitResult = 'Committed'
    )

    @"
SESSION_LAUNCH role=Solo result=Ready
PRODUCT_FLOW role=Solo state=Hut result=Started
PRODUCT_FLOW role=Solo state=Mission result=Started
MISSION_EVENT type=Completed roundId=$RoundId peerId=0
PRODUCT_FLOW role=Solo state=Home result=Completed
SAVE_COMMIT role=Solo roundId=$RoundId slotId=$Slot missionId=$Mission result=$CommitResult
SESSION_SHUTDOWN result=Completed managerCount=0 reason=None
"@
}

function New-NetworkFixture {
    param([int] $Players)

    $hostLines = [Collections.Generic.List[string]]::new()
    $hostLines.Add('SESSION_LAUNCH role=Host result=Ready')
    $hostLines.Add('PRODUCT_FLOW role=Host state=Hut result=Started')
    $hostLines.Add('SESSION_STATE state=Hosting role=Host')
    for ($peer = 0; $peer -lt $Players; $peer++) {
        $hostLines.Add("SESSION_READY peerId=$peer scene=Mission roundId=$roundA")
    }
    $hostLines.Add("MISSION_START_AUTHORIZED roundId=$roundA players=$Players")
    $hostLines.Add("MISSION_EVENT type=Completed roundId=$roundA peerId=0")
    $hostLines.Add('PRODUCT_FLOW role=Host state=Home result=Completed')
    $hostLines.Add("SAVE_COMMIT role=Host roundId=$roundA slotId=$slotId missionId=$missionId result=Committed")
    $hostLines.Add('SESSION_SHUTDOWN result=Completed managerCount=0 reason=None')

    $logs = @{ host = ($hostLines -join "`n") }
    for ($peer = 1; $peer -lt $Players; $peer++) {
        $guestLines = [Collections.Generic.List[string]]::new()
        $guestLines.Add('SESSION_LAUNCH role=Guest result=Ready')
        $guestLines.Add('PRODUCT_FLOW role=Guest state=Hut result=Started')
        $guestLines.Add('SESSION_STATE state=Joining role=Guest')
        $guestLines.Add("SESSION_READY peerId=$peer scene=Mission roundId=$roundA")
        $guestLines.Add("MISSION_EVENT type=Completed roundId=$roundA peerId=0")
        $guestLines.Add('PRODUCT_FLOW role=Guest state=Home result=Completed')
        $guestLines.Add('SESSION_SHUTDOWN result=Completed managerCount=0 reason=None')
        $logs["guest$peer"] = ($guestLines -join "`n")
    }
    $logs
}

function New-InvalidJoinFixture {
    @"
SESSION_LAUNCH role=Guest result=Failed error=InvalidEndpoint
SESSION_JOIN_REJECT reason=InvalidEndpoint
PRODUCT_FLOW role=Guest state=Home result=Failed error=InvalidEndpoint
SESSION_SHUTDOWN result=Completed managerCount=0 reason=None
"@
}

function New-RejoinFixture {
    @{
        host   = @"
SESSION_LAUNCH role=Host result=Ready
PRODUCT_FLOW role=Host state=Hut result=Started
SESSION_STATE state=Hosting role=Host
"@
        guest1 = @"
SESSION_LAUNCH role=Guest result=Ready
PRODUCT_FLOW role=Guest state=Hut result=Started
"@
        guest2 = @"
SESSION_LAUNCH role=Guest result=Ready
PRODUCT_FLOW role=Guest state=Hut result=Started
"@
    }
}

function New-FullHutFixture {
    $logs = @{
        host = @"
SESSION_LAUNCH role=Host result=Ready
PRODUCT_FLOW role=Host state=Hut result=Started
SESSION_STATE state=Hosting role=Host
"@
    }
    for ($peer = 1; $peer -le 3; $peer++) {
        $logs["guest$peer"] = @"
SESSION_LAUNCH role=Guest result=Ready
PRODUCT_FLOW role=Guest state=Hut result=Started
"@
    }
    $logs['guest4'] = @"
SESSION_LAUNCH role=Guest result=Failed error=SessionFull
SESSION_JOIN_REJECT reason=SessionFull
PRODUCT_FLOW role=Guest state=Home result=Failed error=SessionFull
"@
    $logs
}

function New-HostLossFixture {
    @{
        host  = @"
SESSION_LAUNCH role=Host result=Ready
PRODUCT_FLOW role=Host state=Hut result=Started
SESSION_STATE state=Hosting role=Host
"@
        guest = @"
SESSION_LAUNCH role=Guest result=Ready
PRODUCT_FLOW role=Guest state=Hut result=Started
SESSION_STATE state=Failed role=Guest
PRODUCT_FLOW role=Guest state=Home result=Failed error=HostLeft
SESSION_SHUTDOWN result=Completed managerCount=0 reason=HostLeft
"@
    }
}

Assert-Check 'catalog marks solo transition as process smoke' ((Get-ProductCaseDefinition 'solo-transition').Kind -eq 'process-smoke')
Assert-Check 'catalog marks manual GUI as manual' ((Get-ProductCaseDefinition 'manual-gui-solo').Kind -eq 'manual')
Assert-Check 'catalog separates manual and process cases' ((Get-ProductProcessSmokeCaseNames).Count -eq 9 -and (Get-ProductManualCaseNames).Count -eq 5)

$solo = New-SoloFixture
$duoFixture = New-NetworkFixture -Players 2
$duo = @{ host = $duoFixture['host']; guest = $duoFixture['guest1'] }
$fourFixture = New-NetworkFixture -Players 4
$four = @{
    host   = $fourFixture['host']
    guest1 = $fourFixture['guest1']
    guest2 = $fourFixture['guest2']
    guest3 = $fourFixture['guest3']
}
$invalidJoin = New-InvalidJoinFixture
$rejoin = New-RejoinFixture
$fullHut = New-FullHutFixture
$hostLoss = New-HostLossFixture

Assert-ProductCase 'solo transition positive' 'solo-transition' @{ solo = $solo }
Assert-ProductCase 'duo transition positive' 'duo-transition' $duo
Assert-ProductCase 'four transition positive' 'four-transition' $four
Assert-ProductCase 'save roundtrip positive' 'save-roundtrip' @{
    'solo-run1' = (New-SoloFixture -RoundId $roundA)
    'solo-run2' = (New-SoloFixture -RoundId $roundB)
}
Assert-ProductCase 'save roundtrip accepts idempotent second commit' 'save-roundtrip' @{
    'solo-run1' = (New-SoloFixture -RoundId $roundA)
    'solo-run2' = (New-SoloFixture -RoundId $roundB -CommitResult 'AlreadyCommitted')
}
Assert-ProductCase 'no client write positive' 'no-client-write' $duo
Assert-ProductCase 'invalid join positive' 'invalid-join' @{ guest = $invalidJoin }
Assert-ProductCase 'rejoin positive' 'rejoin' $rejoin
Assert-ProductCase 'full hut positive' 'full-hut' $fullHut
Assert-ProductCase 'host loss positive' 'host-loss' $hostLoss

$soloDuplicateLaunch = $solo + "`nSESSION_LAUNCH role=Solo result=Ready"
$soloDuplicateCompleted = $solo + "`nMISSION_EVENT type=Completed roundId=$roundA peerId=0"
$soloDuplicateSave = $solo + "`nSAVE_COMMIT role=Solo roundId=$roundA slotId=$slotId missionId=$missionId result=Committed"
$soloManagerLeak = $solo.Replace('managerCount=0', 'managerCount=1')
$soloMissingShutdown = $solo.Replace("`nSESSION_SHUTDOWN result=Completed managerCount=0 reason=None", '')
$soloRejectedCommit = $solo.Replace('result=Committed', 'result=Rejected reason=CommitFailed')
$soloPrefixedOnly = @"
XPRODUCT_FLOW role=Solo state=Home result=Completed
XSESSION_LAUNCH role=Solo result=Ready
XSAVE_COMMIT role=Solo roundId=$roundA slotId=$slotId missionId=$missionId result=Committed
"@
$soloLaterFalseShutdown = $solo + "`nSESSION_SHUTDOWN result=Failed managerCount=1 reason=Disconnected"

$duoMissingGuestReady = @{
    host  = $duo['host'].Replace("`nSESSION_READY peerId=1 scene=Mission roundId=$roundA", '')
    guest = $duo['guest'].Replace("`nSESSION_READY peerId=1 scene=Mission roundId=$roundA", '')
}
$duoWrongPlayers = @{
    host  = $duo['host'].Replace('players=2', 'players=3')
    guest = $duo['guest']
}
$duoRoundMismatch = @{
    host  = $duo['host']
    guest = $duo['guest'].Replace("SESSION_READY peerId=1 scene=Mission roundId=$roundA", "SESSION_READY peerId=1 scene=Mission roundId=$roundB")
}
$duoMissingGuestShutdown = @{
    host  = $duo['host']
    guest = $duo['guest'].Replace("`nSESSION_SHUTDOWN result=Completed managerCount=0 reason=None", '')
}
$duoDuplicateHostCompletion = @{
    host  = $duo['host'] + "`nMISSION_EVENT type=Completed roundId=$roundA peerId=0"
    guest = $duo['guest']
}
$duoDuplicateReady = @{
    host  = $duo['host'] + "`nSESSION_READY peerId=1 scene=Mission roundId=$roundA"
    guest = $duo['guest']
}

$noClientWriteGuestSave = @{
    host  = $duo['host']
    guest = $duo['guest'] + "`nSAVE_COMMIT role=Guest roundId=$roundA slotId=$slotId missionId=$missionId result=Committed"
}

$invalidJoinBecameReady = $invalidJoin.Replace(
    'SESSION_LAUNCH role=Guest result=Failed error=InvalidEndpoint',
    'SESSION_LAUNCH role=Guest result=Ready')
$invalidJoinDuplicateReject = $invalidJoin + "`nSESSION_JOIN_REJECT reason=InvalidEndpoint"

$rejoinMissionStart = @{
    host   = $rejoin['host'] + "`nMISSION_START_AUTHORIZED roundId=$roundA players=2"
    guest1 = $rejoin['guest1']
    guest2 = $rejoin['guest2']
}
$rejoinGuestSave = @{
    host   = $rejoin['host']
    guest1 = $rejoin['guest1']
    guest2 = $rejoin['guest2'] + "`nSAVE_COMMIT role=Guest roundId=$roundA slotId=$slotId missionId=$missionId result=Committed"
}

$fullHutNoReject = @{
    host   = $fullHut['host']
    guest1 = $fullHut['guest1']
    guest2 = $fullHut['guest2']
    guest3 = $fullHut['guest3']
    guest4 = @"
SESSION_LAUNCH role=Guest result=Ready
PRODUCT_FLOW role=Guest state=Hut result=Started
"@
}
$fullHutWrongReason = @{
    host   = $fullHut['host']
    guest1 = $fullHut['guest1']
    guest2 = $fullHut['guest2']
    guest3 = $fullHut['guest3']
    guest4 = $fullHut['guest4'].Replace('SessionFull', 'SessionNotFound')
}

$hostLossNoHostLeft = @{
    host  = $hostLoss['host']
    guest = $hostLoss['guest'].Replace('HostLeft', 'Disconnected')
}
$hostLossGuestCompleted = @{
    host  = $hostLoss['host']
    guest = $hostLoss['guest'] + "`nMISSION_EVENT type=Completed roundId=$roundA peerId=1"
}

$saveDifferentSlot = @{
    'solo-run1' = (New-SoloFixture -RoundId $roundA)
    'solo-run2' = (New-SoloFixture -RoundId $roundB -Slot '44444444-4444-4444-8444-444444444444')
}
$saveSameRound = @{
    'solo-run1' = (New-SoloFixture -RoundId $roundA)
    'solo-run2' = (New-SoloFixture -RoundId $roundA)
}
$saveSecondRejected = @{
    'solo-run1' = (New-SoloFixture -RoundId $roundA)
    'solo-run2' = (New-SoloFixture -RoundId $roundB).Replace('result=Committed', 'result=Rejected reason=CommitFailed')
}
$saveMissingRun = @{ 'solo-run1' = (New-SoloFixture -RoundId $roundA) }

Assert-ProductCase 'duplicate launch fails' 'solo-transition' @{ solo = $soloDuplicateLaunch } $false
Assert-ProductCase 'duplicate completion fails' 'solo-transition' @{ solo = $soloDuplicateCompleted } $false
Assert-ProductCase 'duplicate save fails' 'solo-transition' @{ solo = $soloDuplicateSave } $false
Assert-ProductCase 'manager leak fails' 'solo-transition' @{ solo = $soloManagerLeak } $false
Assert-ProductCase 'missing shutdown fails' 'solo-transition' @{ solo = $soloMissingShutdown } $false
Assert-ProductCase 'rejected commit fails' 'solo-transition' @{ solo = $soloRejectedCommit } $false
Assert-ProductCase 'prefixed marker cannot pass by substring' 'solo-transition' @{ solo = $soloPrefixedOnly } $false
Assert-ProductCase 'later false shutdown fails' 'solo-transition' @{ solo = $soloLaterFalseShutdown } $false
Assert-ProductCase 'missing guest readiness fails' 'duo-transition' $duoMissingGuestReady $false
Assert-ProductCase 'wrong authorized player count fails' 'duo-transition' $duoWrongPlayers $false
Assert-ProductCase 'mixed ready rounds fail' 'duo-transition' $duoRoundMismatch $false
Assert-ProductCase 'missing guest shutdown fails' 'duo-transition' $duoMissingGuestShutdown $false
Assert-ProductCase 'duplicate host completion fails' 'duo-transition' $duoDuplicateHostCompletion $false
Assert-ProductCase 'duplicate ready peer fails' 'duo-transition' $duoDuplicateReady $false
Assert-ProductCase 'guest save fails no-client-write' 'no-client-write' $noClientWriteGuestSave $false
Assert-ProductCase 'changed sentinel fails no-client-write' 'no-client-write' $duo $false $false
Assert-ProductCase 'failed join that becomes ready fails' 'invalid-join' @{ guest = $invalidJoinBecameReady } $false
Assert-ProductCase 'duplicate join rejection fails' 'invalid-join' @{ guest = $invalidJoinDuplicateReject } $false
Assert-ProductCase 'rejoin with mission start fails' 'rejoin' $rejoinMissionStart $false
Assert-ProductCase 'rejoin guest save fails' 'rejoin' $rejoinGuestSave $false
Assert-ProductCase 'full hut without rejection fails' 'full-hut' $fullHutNoReject $false
Assert-ProductCase 'full hut wrong rejection reason fails' 'full-hut' $fullHutWrongReason $false
Assert-ProductCase 'host loss without HostLeft fails' 'host-loss' $hostLossNoHostLeft $false
Assert-ProductCase 'host loss with guest completion fails' 'host-loss' $hostLossGuestCompleted $false
Assert-ProductCase 'save roundtrip slot change fails' 'save-roundtrip' $saveDifferentSlot $false
Assert-ProductCase 'save roundtrip reused round fails' 'save-roundtrip' $saveSameRound $false
Assert-ProductCase 'save roundtrip rejected second run fails' 'save-roundtrip' $saveSecondRejected $false
Assert-ProductCase 'save roundtrip missing second run fails' 'save-roundtrip' $saveMissingRun $false

Write-Output 'PARSER_CHECKS=PASS'
