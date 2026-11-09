$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'coop-regression-log-checks.ps1')

function Assert-Check([string] $Name, [bool] $Condition) {
    if (-not $Condition) { throw "Parser check failed: $Name" }
    Write-Output "CHECK_PASS $Name"
}

$giftPassing = @'
GIFT_TEST_DISCONNECT clientId=1 autoCleanup=True forceRelease=True
GIFT_TEST_REGRAB afterDisconnect=True lightGrips=1
GIFT_TEST_RESULT heavyTwoSlots=True release=True lightSingleSlot=True lightPeak=1 separateGifts=True soloHeavySpeedPeak=0.000 soloHeavySpeedBound=True
GIFT_TEST_SINGLE_SLOT_RESULT owner=0 hasSlot=True gripCount=1
GIFT_TEST_SINGLE_SLOT_RESULT owner=7 hasSlot=False gripCount=1
'@
$giftFalseCleanup = $giftPassing.Replace('autoCleanup=True', 'autoCleanup=False')
$giftFalseRegrab = $giftPassing.Replace('afterDisconnect=True lightGrips=1', 'afterDisconnect=False lightGrips=0').Replace('GIFT_TEST_REGRAB_FAIL', '')
$giftLaterFalseCleanup = $giftPassing + "`nGIFT_TEST_DISCONNECT clientId=2 autoCleanup=False forceRelease=True"
$giftLaterFalseRegrab = $giftPassing + "`nGIFT_TEST_REGRAB afterDisconnect=False lightGrips=0"
Assert-Check 'gift positive markers pass' (Test-GiftRegressionLog $giftPassing).Passed
Assert-Check 'false cleanup does not pass by substring' (-not (Test-GiftRegressionLog $giftFalseCleanup).Passed)
Assert-Check 'false regrab does not pass by substring' (-not (Test-GiftRegressionLog $giftFalseRegrab).Passed)
Assert-Check 'later false cleanup does not hide behind a positive marker' (-not (Test-GiftRegressionLog $giftLaterFalseCleanup).Passed)
Assert-Check 'later false regrab does not hide behind a positive marker' (-not (Test-GiftRegressionLog $giftLaterFalseRegrab).Passed)

$fourPlayerPassing = @'
MISSION_PLAYER owner=7 localView=True position=(0.00, 0.00, 0.00)
MISSION_PLAYER owner=4 localView=False position=(0.00, 0.00, 0.00)
MISSION_PLAYER owner=8 localView=False position=(0.00, 0.00, 0.00)
MISSION_PLAYER owner=9 localView=False position=(0.00, 0.00, 0.00)
COOP_PLAYER_START owner=7 slot=0 position=(-0.80, 0.05, -6.25)
COOP_PLAYER_START owner=4 slot=1 position=(0.80, 0.05, -6.25)
COOP_PLAYER_START owner=8 slot=2 position=(-1.50, 0.05, -6.60)
COOP_PLAYER_START owner=9 slot=3 position=(1.50, 0.05, -6.60)
COOP_PLAYER_REPLICA_POSITION owner=7 slot=0 position=(-0.80, 0.05, -6.25)
COOP_PLAYER_REPLICA_POSITION owner=4 slot=1 position=(0.80, 0.05, -6.25)
COOP_PLAYER_REPLICA_POSITION owner=8 slot=2 position=(-1.50, 0.05, -6.60)
COOP_PLAYER_REPLICA_POSITION owner=9 slot=3 position=(1.50, 0.05, -6.60)
COOP_FOUR_PLAYER_RESET_SMOKE started=true players=4
COOP_PLAYER_REPLICA_POSITION owner=7 slot=0 position=(-0.80, 0.05, -6.25)
COOP_PLAYER_REPLICA_POSITION owner=4 slot=1 position=(0.80, 0.05, -6.25)
COOP_PLAYER_REPLICA_POSITION owner=8 slot=2 position=(-1.50, 0.05, -6.60)
COOP_PLAYER_REPLICA_POSITION owner=9 slot=3 position=(1.50, 0.05, -6.60)
'@
$fourPlayerSameSlot = $fourPlayerPassing.Replace('COOP_PLAYER_START owner=4 slot=1', 'COOP_PLAYER_START owner=4 slot=0').Replace('COOP_PLAYER_REPLICA_POSITION owner=4 slot=1', 'COOP_PLAYER_REPLICA_POSITION owner=4 slot=0')
$fourPlayerSamePose = $fourPlayerPassing.Replace('COOP_PLAYER_REPLICA_POSITION owner=9 slot=3 position=(1.50, 0.05, -6.60)', 'COOP_PLAYER_REPLICA_POSITION owner=9 slot=3 position=(-0.80, 0.05, -6.25)')
$fourPlayerLegacyMarkers = $fourPlayerPassing -replace 'COOP_PLAYER_START owner=\d+ slot=\d+ ', 'COOP_PLAYER_START owner=0 ' -replace 'COOP_PLAYER_REPLICA_POSITION owner=\d+ slot=\d+ ', 'MISSION_PLAYER owner=0 '
Assert-Check 'four-player flexible owners, slots and host replicas pass' (Test-FourPlayerLog $fourPlayerPassing).Passed
Assert-Check 'duplicate active slot fails' (-not (Test-FourPlayerLog $fourPlayerSameSlot).Passed)
Assert-Check 'duplicate host replica pose fails' (-not (Test-FourPlayerLog $fourPlayerSamePose).Passed)
Assert-Check 'legacy marker union cannot pass' (-not (Test-FourPlayerLog $fourPlayerLegacyMarkers).Passed)

Write-Output 'PARSER_CHECKS=PASS'
