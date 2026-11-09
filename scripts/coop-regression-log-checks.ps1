function Test-GiftRegressionLog {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $LogText)

    $issues = [Collections.Generic.List[string]]::new()
    if ($LogText -match 'GIFT_TEST_REGRAB_FAIL') { $issues.Add('explicit regrab failure marker present') }

    $disconnects = [regex]::Matches($LogText, 'GIFT_TEST_DISCONNECT clientId=(\d+) autoCleanup=(True|False) forceRelease=(True|False)')
    if ($disconnects.Count -eq 0) { $issues.Add('disconnect result marker missing') }
    foreach ($disconnect in $disconnects) {
        if ($disconnect.Groups[2].Value -ne 'True' -or $disconnect.Groups[3].Value -ne 'True') { $issues.Add('disconnect cleanup values are not both True') }
    }

    $regrabs = [regex]::Matches($LogText, 'GIFT_TEST_REGRAB afterDisconnect=(True|False) lightGrips=(\d+)')
    if ($regrabs.Count -eq 0) { $issues.Add('regrab result marker missing') }
    foreach ($regrab in $regrabs) {
        if ($regrab.Groups[1].Value -ne 'True' -or $regrab.Groups[2].Value -ne '1') { $issues.Add('regrab did not produce exactly one grip') }
    }

    $summary = [regex]::Match($LogText, 'GIFT_TEST_RESULT heavyTwoSlots=(True|False) release=(True|False) lightSingleSlot=(True|False) lightPeak=(\d+) separateGifts=(True|False) soloHeavySpeedPeak=([^\s]+) soloHeavySpeedBound=(True|False)')
    if (-not $summary.Success) { $issues.Add('gift summary marker missing') }
    else {
        foreach ($index in 1, 2, 3, 5, 7) {
            if ($summary.Groups[$index].Value -ne 'True') { $issues.Add("gift summary value $index is not True") }
        }
        if ([int]$summary.Groups[4].Value -gt 1) { $issues.Add('light gift exceeded its one-slot limit') }
    }

    $slotResults = [regex]::Matches($LogText, 'GIFT_TEST_SINGLE_SLOT_RESULT owner=(\d+) hasSlot=(True|False) gripCount=(\d+)')
    $slotByOwner = @{}
    foreach ($result in $slotResults) {
        $slotByOwner[$result.Groups[1].Value] = $result.Groups[2].Value + ':' + $result.Groups[3].Value
    }
    $slotValues = @($slotByOwner.Values | Sort-Object -Unique)
    if ($slotResults.Count -ne 2 -or $slotByOwner.Count -ne 2 -or $slotValues.Count -ne 2 -or $slotValues -notcontains 'True:1' -or $slotValues -notcontains 'False:1') {
        $issues.Add('single-slot result does not show two distinct owners with one winner and one rejected request')
    }

    [pscustomobject]@{ Passed = ($issues.Count -eq 0); Issues = @($issues) }
}

function Test-FourPlayerLog {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $HostLog)

    $issues = [Collections.Generic.List[string]]::new()
    $lines = $HostLog -split "`r?`n"
    $activeOwners = @{}
    $activeSlots = @{}
    $fourPlayerSnapshot = $null
    foreach ($line in $lines) {
        if ($line -match 'COOP_PLAYER_SLOT_RELEASED owner=(\d+) slot=(\d+)') {
            $owner = $Matches[1]; $slot = $Matches[2]
            if ($activeOwners.ContainsKey($owner) -and $activeOwners[$owner] -eq $slot) { $activeOwners.Remove($owner) }
            if ($activeSlots.ContainsKey($slot) -and $activeSlots[$slot] -eq $owner) { $activeSlots.Remove($slot) }
            continue
        }
        if ($line -notmatch 'COOP_PLAYER_START owner=(\d+) slot=(\d+) position=\(([^)\r\n]+)\)') { continue }
        $owner = $Matches[1]; $slot = $Matches[2]; $position = $Matches[3] -replace '\s', ''
        if ($activeOwners.ContainsKey($owner)) {
            if ($activeOwners[$owner] -ne $slot) { $issues.Add("owner $owner changed slot before a release") }
            continue
        }
        if ($activeSlots.ContainsKey($slot) -and $activeSlots[$slot] -ne $owner) {
            $issues.Add("slot $slot was assigned to multiple active owners")
            continue
        }
        $activeOwners[$owner] = $slot
        $activeSlots[$slot] = $owner
        if ($activeOwners.Count -ge 4 -and $null -eq $fourPlayerSnapshot) { $fourPlayerSnapshot = @{} + $activeOwners }
    }

    if ($null -eq $fourPlayerSnapshot) { $issues.Add('no point with four distinct active owners was found') }
    else {
        $slots = @($fourPlayerSnapshot.Values | Sort-Object -Unique)
        if ($slots.Count -ne 4 -or @('0', '1', '2', '3' | Where-Object { $slots -notcontains $_ }).Count -gt 0) {
            $issues.Add('the four active owners do not occupy the four distinct slots 0-3')
        }

        $resetSmokeLines = [Collections.Generic.List[int]]::new()
        $resetCompleteLines = [Collections.Generic.List[int]]::new()
        $resetCompleteMarkers = [regex]::Matches($HostLog, 'COOP_HOST_RESET complete=(?i:True|False)')
        for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
            if ($lines[$lineIndex] -match 'COOP_FOUR_PLAYER_RESET_SMOKE started=True players=4') { $resetSmokeLines.Add($lineIndex) }
            if ($lines[$lineIndex] -match 'COOP_HOST_RESET complete=(True|False)') {
                $resetCompleteLines.Add($lineIndex)
                if ($Matches[1] -ne 'true') { $issues.Add('host reset completion marker is not true') }
            }
        }
        if ($resetSmokeLines.Count -ne 1) { $issues.Add('four-player reset smoke marker is missing or ambiguous') }
        if ($resetCompleteMarkers.Count -ne 1 -or $resetCompleteLines.Count -ne 1) { $issues.Add('host reset completion marker is missing or ambiguous') }
        $resetSmokeLine = if ($resetSmokeLines.Count -eq 1) { $resetSmokeLines[0] } else { -1 }
        $resetCompleteLine = if ($resetCompleteLines.Count -eq 1) { $resetCompleteLines[0] } else { -1 }
        if ($resetSmokeLine -lt 0 -or $resetCompleteLine -le $resetSmokeLine) { $issues.Add('host reset did not complete after the four-player reset smoke began') }

        $startsByOwner = @{}
        foreach ($line in $lines) {
            if ($line -match 'COOP_PLAYER_START owner=(\d+) slot=(\d+) position=\(([^)\r\n]+)\)') {
                $owner = $Matches[1]
                if ($fourPlayerSnapshot.ContainsKey($owner) -and -not $startsByOwner.ContainsKey($owner)) {
                    $startsByOwner[$owner] = [pscustomobject]@{ Slot = $Matches[2]; Position = ($Matches[3] -replace '\s', '') }
                }
            }
        }

        $replicasByOwner = @{}
        if ($resetSmokeLine -ge 0) {
            for ($lineIndex = 0; $lineIndex -lt $resetSmokeLine; $lineIndex++) {
                $line = $lines[$lineIndex]
                if ($line -match 'COOP_PLAYER_REPLICA_POSITION owner=(\d+) slot=(\d+) position=\(([^)\r\n]+)\)') {
                    $owner = $Matches[1]
                    if ($fourPlayerSnapshot.ContainsKey($owner) -and -not $replicasByOwner.ContainsKey($owner)) {
                        $replicasByOwner[$owner] = [pscustomobject]@{ Slot = $Matches[2]; Position = ($Matches[3] -replace '\s', '') }
                    }
                }
            }
        }

        if ($replicasByOwner.Count -ne 4) { $issues.Add('four distinct host replica pose markers are required before reset begins') }
        foreach ($owner in $fourPlayerSnapshot.Keys) {
            if (-not $startsByOwner.ContainsKey($owner) -or -not $replicasByOwner.ContainsKey($owner)) { continue }
            if ($startsByOwner[$owner].Slot -ne $fourPlayerSnapshot[$owner] -or $replicasByOwner[$owner].Slot -ne $fourPlayerSnapshot[$owner]) {
                $issues.Add("owner $owner start and host replica slots do not match the active assignment")
            }
            if ($startsByOwner[$owner].Position -ne $replicasByOwner[$owner].Position) {
                $issues.Add("owner $owner assigned start and actual pre-reset host replica positions do not match")
            }
        }
        if (@($startsByOwner.Values.Position | Sort-Object -Unique).Count -ne 4) { $issues.Add('authoritative start positions are not distinct') }
        if (@($replicasByOwner.Values.Position | Sort-Object -Unique).Count -ne 4) { $issues.Add('actual pre-reset host replica positions are not distinct') }

        $postResetReplicas = @{}
        if ($resetCompleteLine -ge 0 -and $resetCompleteLine -gt $resetSmokeLine) {
            for ($lineIndex = $resetCompleteLine + 1; $lineIndex -lt $lines.Count; $lineIndex++) {
                $line = $lines[$lineIndex]
                if ($line -match 'COOP_PLAYER_REPLICA_POSITION owner=(\d+) slot=(\d+) position=\(([^)\r\n]+)\)') {
                $owner = $Matches[1]
                    if ($fourPlayerSnapshot.ContainsKey($owner) -and -not $postResetReplicas.ContainsKey($owner)) {
                        $postResetReplicas[$owner] = [pscustomobject]@{ Slot = $Matches[2]; Position = ($Matches[3] -replace '\s', '') }
                    }
                }
            }
        }
        if ($postResetReplicas.Count -ne 4 -or @($postResetReplicas.Values.Position | Sort-Object -Unique).Count -ne 4) {
            $issues.Add('four distinct actual host replica positions are required after reset completion')
        }
        foreach ($owner in $fourPlayerSnapshot.Keys) {
            if ($postResetReplicas.ContainsKey($owner) -and $postResetReplicas[$owner].Slot -ne $fourPlayerSnapshot[$owner]) {
                $issues.Add("owner $owner changed slot after the four-player reset")
            }
            if ($postResetReplicas.ContainsKey($owner) -and $startsByOwner.ContainsKey($owner) -and
                $postResetReplicas[$owner].Position -ne $startsByOwner[$owner].Position) {
                $issues.Add("owner $owner was not reset to its assigned start position")
            }
        }

        $missionOwners = @{}
        foreach ($line in $lines) {
            if ($line -match 'MISSION_PLAYER owner=(\d+) localView=(True|False)') { $missionOwners[$Matches[1]] = $Matches[2] }
        }
        $localOwners = @($missionOwners.Keys | Where-Object { $missionOwners[$_] -eq 'True' })
        if ($localOwners.Count -ne 1 -or -not $fourPlayerSnapshot.ContainsKey($localOwners[0])) {
            $issues.Add('host local camera owner is not uniquely present among the four players')
        }
        else {
            foreach ($owner in $fourPlayerSnapshot.Keys) {
                $expected = if ($owner -eq $localOwners[0]) { 'True' } else { 'False' }
                if (-not $missionOwners.ContainsKey($owner) -or $missionOwners[$owner] -ne $expected) {
                    $issues.Add("owner $owner camera ownership does not match the host replica log")
                }
            }
        }
    }

    [pscustomobject]@{ Passed = ($issues.Count -eq 0); Issues = @($issues) }
}
