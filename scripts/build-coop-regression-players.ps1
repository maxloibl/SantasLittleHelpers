param(
    [string] $UnityPath = 'C:\6000.6.8f1\Editor\Unity.exe',
    [string] $GiftOutput = 'C:\tmp\slh14-build\GiftCoopTest.exe',
    [string] $BuildLogRoot = 'C:\tmp\slh14-build-logs'
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$unity = (Resolve-Path -LiteralPath $UnityPath).Path
New-Item -ItemType Directory -Path $BuildLogRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $GiftOutput) -Force | Out-Null
$generatedPaths = @(
    'Assets/Game/Gifts/Tests/GiftCoopTestPlayer.prefab',
    'Assets/Game/Gifts/Tests/Scenes/GiftCoopNetworkTest.unity',
    'Assets/Game/Gifts/Tests/GiftCoopNetworkPrefabs.asset',
    'Assets/Game/Capture/CaptureTestPlayer.prefab',
    'Assets/Game/Capture/Scenes/CaptureTest.unity',
    'Assets/DefaultNetworkPrefabs.asset',
    'Assets/DefaultNetworkPrefabs.asset.meta',
    'Assets/Settings/PC_RPAsset.asset',
    'Assets/Settings/UniversalRenderPipelineGlobalSettings.asset',
    'ProjectSettings/GraphicsSettings.asset',
    'ProjectSettings/ProjectAuditorSettings.asset',
    'ProjectSettings/ShaderGraphSettings.asset',
    'ProjectSettings/UnityConnectSettings.asset'
)
$backupRoot = Join-Path $BuildLogRoot ('source-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$originalFiles = @{}
foreach ($relativePath in $generatedPaths) {
    $fullPath = Join-Path $projectPath $relativePath
    if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
        $backupPath = Join-Path $backupRoot $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $backupPath) -Force | Out-Null
        Copy-Item -LiteralPath $fullPath -Destination $backupPath
        $originalFiles[$relativePath] = $true
    }
}

function Invoke-UnityBuild([string] $Method, [string] $LogName, [string] $ExpectedMarker) {
    $logPath = Join-Path $BuildLogRoot $LogName
    $arguments = @('-batchmode', '-nographics', '-projectPath', $projectPath, '-executeMethod', $Method, '-quit', '-logFile', $logPath)
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $unity
    $startInfo.Arguments = (($arguments | ForEach-Object { Quote-UnityArgument $_ }) -join ' ')
    $startInfo.WorkingDirectory = $projectPath
    $startInfo.UseShellExecute = $true
    $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $process = [Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) { throw "Could not start Unity at $unity" }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity build failed ($($process.ExitCode)); inspect $logPath" }
    $log = Get-Content -LiteralPath $logPath -Raw
    if ($log -notmatch [regex]::Escape($ExpectedMarker)) { throw "Expected build marker '$ExpectedMarker' missing; inspect $logPath" }
    if ($log -match 'errors=[1-9][0-9]*') { throw "Unity reported build errors; inspect $logPath" }
    Write-Output "BUILD_PASS method=$Method log=$logPath"
}

function Quote-UnityArgument([string] $Value) {
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

try {
    $env:SLH9_TEST_OUTPUT = $GiftOutput
    Invoke-UnityBuild 'SantasLittleHelpers.Editor.GiftCoopNetworkTestSceneBuilder.BuildWindowsPlayer' 'gift-player.log' 'GIFT_TEST_BUILD result=Succeeded'
    Remove-Item Env:\SLH9_TEST_OUTPUT -ErrorAction SilentlyContinue
    Invoke-UnityBuild 'SantasLittleHelpers.Capture.Editor.CaptureTestSceneBuilder.BuildWindowsSmokePlayer' 'capture-player.log' 'CAPTURE_WINDOWS_BUILD result=Succeeded'
}
finally {
    Remove-Item Env:\SLH9_TEST_OUTPUT -ErrorAction SilentlyContinue
    foreach ($relativePath in $generatedPaths) {
        $fullPath = Join-Path $projectPath $relativePath
        if ($originalFiles.ContainsKey($relativePath)) {
            Copy-Item -LiteralPath (Join-Path $backupRoot $relativePath) -Destination $fullPath -Force
        }
        elseif (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            Remove-Item -LiteralPath $fullPath -Force
        }
    }
}
Write-Output "GIFT_BUILD=$GiftOutput"
Write-Output "CAPTURE_BUILD=$projectPath\Builds\CaptureSmoke\CaptureSmoke.exe"
