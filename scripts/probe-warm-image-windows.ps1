Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$fixed='92e3548845bd07ee7717b53df9588c412d128f9d'
$parent='ab3bf269f956a7b19f6d8ead1383ff80495ad95c'
$repo=Split-Path $PSScriptRoot -Parent
$report=[ordered]@{Kind='WarmImageWindowsPrerequisites';Status='Blocked';Stage='HostedContext';FixedProductSource=$fixed;DeliverySource=$null;Reason='PreparationFailure';Token=$null;Desktop=$null;SourceInputs=@();ProductionAuthentication='NotRun';SecondCliToSameWpf='NotRun';WorkerExitBeforePreview='NotRun';AclRefusal='NotRun';CancellationAndOwner='NotRun';ExplorerDesktopAcceptance='NotRun';Package='Hold';ProductionReuse='Off';ModelCalls=0;SecurityChanges=0;SdkExecution='NotRun';ProductLaunch='NotRun'}
$code=2
try {
    if($env:GITHUB_ACTIONS -cne 'true' -or $env:GITHUB_REPOSITORY -cne 'T1doo/KuaiYi' -or $env:GITHUB_REF -cne 'refs/heads/dev/p7-fixed-signoff' -or $env:GITHUB_EVENT_NAME -cne 'push' -or $env:GITHUB_RUN_ATTEMPT -cne '1' -or $env:RUNNER_OS -cne 'Windows' -or [IntPtr]::Size -ne 8){throw 'Hosted context refused'}
    if($env:GITHUB_SHA -cnotmatch '^[0-9a-f]{40}$'){throw 'Event source refused'}
    if((Get-Item -LiteralPath $env:GITHUB_EVENT_PATH).Length -gt 1048576){throw 'Event bound refused'}
    $event=Get-Content -LiteralPath $env:GITHUB_EVENT_PATH -Raw | ConvertFrom-Json
    if($event.repository.private -cne $false){throw 'Public standard runner required'}
    $report.Stage='SourceIdentity'
    $head=(& git -C $repo rev-parse HEAD).Trim();if($LASTEXITCODE -ne 0 -or $head -cne $env:GITHUB_SHA){throw 'Source identity refused'}
    $parents=(& git -C $repo rev-list --parents -n 1 HEAD).Trim();if($LASTEXITCODE -ne 0 -or $parents -cne "$head $parent"){throw 'Reviewed parent refused'}
    $expected=@('.github/workflows/windows-warm-image.yml','docs/progress.md','docs/validation/warm-image-windows.md','scripts/probe-warm-image-windows.ps1','tools/ci/WarmImageWindowsFacts.cs') | Sort-Object
    $changed=@(& git -C $repo diff --name-only $parent HEAD -- | Sort-Object);if($LASTEXITCODE -ne 0 -or ($changed -join "`n") -cne ($expected -join "`n")){throw 'Reviewed scope refused'}
    $dirty=@(& git -C $repo status --porcelain=v1);if($LASTEXITCODE -ne 0 -or $dirty.Count -ne 0){throw 'Dirty checkout refused'}
    & git -C $repo diff --quiet $fixed HEAD -- src tests packaging global.json Directory.Build.props Directory.Packages.props NuGet.Config
    if($LASTEXITCODE -ne 0){throw 'Fixed product inputs changed'}
    $report.DeliverySource=$head
    $report.Stage='SourceInventory'
    foreach($relative in @('src/KuaiYi.App/Program.cs','src/KuaiYi.App/IsolatedSessionLayout.cs','src/KuaiYi.App/WindowsInstallationImagePeer.cs','src/KuaiYi.App/WindowsInstallationImagePipe.cs','src/KuaiYi.Core/Installation/InstallationPeerPolicy.cs','tools/ci/WindowsFacts.cs','tools/ci/WarmImageWindowsFacts.cs','scripts/probe-warm-image-windows.ps1')){
        $report.SourceInputs+=@{Path=$relative;CheckoutSha256=(Get-FileHash -LiteralPath (Join-Path $repo $relative) -Algorithm SHA256).Hash.ToLowerInvariant()}
    }
    $report.Stage='NativeInventoryCompile'
    Add-Type -Path (Join-Path $repo 'tools/ci/WindowsFacts.cs')
    Add-Type -Path (Join-Path $repo 'tools/ci/WarmImageWindowsFacts.cs')
    $report.Stage='OwnAndLinkedTokenRead'
    $report.Token=[KuaiYiWarmImageWindowsFacts]::Capture()
    $report.Stage='DesktopRead'
    $os=[KuaiYiCiWindowsFacts]::ReadOs()
    $report.Desktop=@{OsBuild=$os.Build;UserInteractive=[Environment]::UserInteractive;WindowStation=[KuaiYiCiWindowsFacts]::WindowStation();InputDesktop=[KuaiYiCiWindowsFacts]::InputDesktop();HasShell=[KuaiYiCiWindowsFacts]::HasShell();DwmEnabled=[KuaiYiCiWindowsFacts]::DwmEnabled();MonitorCount=@([KuaiYiCiWindowsFacts]::Monitors()).Count;ImageVersion=$env:ImageVersion}
    $report.Stage='Completed';$report.Status='InventoryComplete_ProductNotRun';$report.Reason='PrerequisitesOnly_NoAuthorizationOrAcceptance'
    $code=0
} catch {
    # No exception message/path, SID, authentication LUID, token or credential in public diagnostics.
    $report.Reason='InventoryFailure';$report.ErrorType=$_.Exception.GetType().Name
}
Write-Host ('KUAIYI_WARM_WINDOWS_PREREQUISITES '+($report | ConvertTo-Json -Depth 8 -Compress))
exit $code
