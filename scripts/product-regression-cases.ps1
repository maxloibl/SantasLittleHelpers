$script:ProductCaseCatalog = [ordered]@{
    'solo-transition' = [pscustomobject]@{
        Name        = 'solo-transition'
        Kind        = 'process-smoke'
        Players     = 1
        Description = 'Solo launch, mission completion, save commit and shutdown without a NetworkManager.'
    }
    'duo-transition' = [pscustomobject]@{
        Name        = 'duo-transition'
        Kind        = 'process-smoke'
        Players     = 2
        Description = 'Host plus one guest reach readiness before one authorized start, then commit once on the host.'
    }
    'four-transition' = [pscustomobject]@{
        Name        = 'four-transition'
        Kind        = 'process-smoke'
        Players     = 4
        Description = 'Host plus three guests reach readiness before one authorized start, then commit once on the host.'
    }
    'save-roundtrip' = [pscustomobject]@{
        Name        = 'save-roundtrip'
        Kind        = 'process-smoke'
        Players     = 1
        Description = 'Two sequential solo runs share one isolated profile and prove the same slot and mission survive a process restart.'
    }
    'no-client-write' = [pscustomobject]@{
        Name        = 'no-client-write'
        Kind        = 'process-smoke'
        Players     = 2
        Description = 'A guest joins a completed host round, never emits SAVE_COMMIT and leaves the sentinel untouched.'
    }
    'invalid-join' = [pscustomobject]@{
        Name        = 'invalid-join'
        Kind        = 'process-smoke'
        Players     = 0
        Description = 'A join with an invalid IPv4 address fails with InvalidEndpoint before any session readiness.'
    }
    'rejoin' = [pscustomobject]@{
        Name        = 'rejoin'
        Kind        = 'process-smoke'
        Players     = 3
        Description = 'A guest leaves in the hut and a second guest joins the still-open host session without a mission start.'
    }
    'full-hut' = [pscustomobject]@{
        Name        = 'full-hut'
        Kind        = 'process-smoke'
        Players     = 4
        Description = 'Three accepted guests fill the hut; the fifth player is rejected with SessionFull.'
    }
    'host-loss' = [pscustomobject]@{
        Name        = 'host-loss'
        Kind        = 'process-smoke'
        Players     = 2
        Description = 'The harness kills its own host process; the guest reports HostLeft, shuts down and never writes a save.'
    }
    'manual-gui-solo' = [pscustomobject]@{
        Name        = 'manual-gui-solo'
        Kind        = 'manual'
        Players     = 1
        Description = 'Visible one-window GUI round with real input, save roundtrip across restart and returned home state.'
    }
    'manual-gui-duo' = [pscustomobject]@{
        Name        = 'manual-gui-duo'
        Kind        = 'manual'
        Players     = 2
        Description = 'Two visible windows: host and join in the hut, one mission, host commit only and host loss.'
    }
    'manual-gui-four' = [pscustomobject]@{
        Name        = 'manual-gui-four'
        Kind        = 'manual'
        Players     = 4
        Description = 'Four visible windows: full hut, rejected fifth join, rejoin, mission completion and host commit only.'
    }
    'manual-lan-duo' = [pscustomobject]@{
        Name        = 'manual-lan-duo'
        Kind        = 'manual'
        Players     = 2
        Description = 'Two machines on one LAN use direct IPv4/UDP without relay, matchmaking or IPv6.'
    }
    'manual-lan-four' = [pscustomobject]@{
        Name        = 'manual-lan-four'
        Kind        = 'manual'
        Players     = 4
        Description = 'Four machines on one LAN exercise capacity, mission readiness, host commit and host loss.'
    }
}

function Get-ProductCaseCatalog {
    $script:ProductCaseCatalog
}

function Get-ProductCaseDefinition {
    param([Parameter(Mandatory = $true)][string] $Name)

    if (-not $script:ProductCaseCatalog.Contains($Name)) {
        throw "Unknown product regression case '$Name'. Use -ListCases to see the supported matrix."
    }
    $script:ProductCaseCatalog[$Name]
}

function Get-ProductProcessSmokeCaseNames {
    @($script:ProductCaseCatalog.Keys | Where-Object { $script:ProductCaseCatalog[$_].Kind -eq 'process-smoke' })
}

function Get-ProductManualCaseNames {
    @($script:ProductCaseCatalog.Keys | Where-Object { $script:ProductCaseCatalog[$_].Kind -eq 'manual' })
}
