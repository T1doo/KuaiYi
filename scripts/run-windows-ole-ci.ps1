param([Parameter(Mandatory)][string]$NativeRoot,[Parameter(Mandatory)][string]$OutputRoot)
. "$PSScriptRoot/windows-ci-common.ps1"
# An ordinary build directory, not a publish/bundle/installer. Use only the already restored, locked projects.
$app = Join-Path $OutputRoot 'app'
[void][IO.Directory]::CreateDirectory($app)
Invoke-Checked dotnet @('restore','src/KuaiYi.OleReceiver/KuaiYi.OleReceiver.csproj','--locked-mode') | ForEach-Object { Write-Host $_ }
Invoke-Checked dotnet @('restore','tools/KuaiYi.OleFaultSource/KuaiYi.OleFaultSource.csproj','--locked-mode') | ForEach-Object { Write-Host $_ }
Invoke-Checked dotnet @('restore','src/KuaiYi.App/KuaiYi.App.csproj','--locked-mode') | ForEach-Object { Write-Host $_ }
Invoke-Checked dotnet @('build','src/KuaiYi.OleReceiver/KuaiYi.OleReceiver.csproj','--configuration','Release','--no-restore') | ForEach-Object { Write-Host $_ }
Invoke-Checked dotnet @('build','tools/KuaiYi.OleFaultSource/KuaiYi.OleFaultSource.csproj','--configuration','Release','--no-restore') | ForEach-Object { Write-Host $_ }
Invoke-Checked dotnet @('build','src/KuaiYi.App/KuaiYi.App.csproj','--configuration','Release','--no-restore','--output',$app,'-p:KuaiYiIncludeWindowsCiProbe=true') | ForEach-Object { Write-Host $_ }
$receiver = Join-Path $script:RepositoryRoot 'src/KuaiYi.OleReceiver/bin/Release/net10.0-windows/win-x64'
foreach ($name in @('KuaiYi.OleReceiver.exe','KuaiYi.OleReceiver.dll','KuaiYi.OleReceiver.runtimeconfig.json','KuaiYi.OleReceiver.deps.json')) { Copy-Item -LiteralPath (Join-Path $receiver $name) -Destination $app }
Copy-Item -LiteralPath (Join-Path $NativeRoot 'KuaiYi.OleNativeBridge/Release/KuaiYi.OleNativeBridge.dll') -Destination $app
$source = Join-Path $script:RepositoryRoot 'tools/KuaiYi.OleFaultSource/bin/Release/net10.0-windows/win-x64/KuaiYi.OleFaultSource.exe'
$receipt = Join-Path $OutputRoot 'ole-report.json'
$info = [Diagnostics.ProcessStartInfo]::new((Join-Path $app 'KuaiYi.App.exe')); $info.UseShellExecute = $false
$info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
foreach ($argument in @('--ci-ole-responsiveness',$receipt,$source,(Join-Path $script:RepositoryRoot 'assets/fixtures/selection-public.txt'))) { [void]$info.ArgumentList.Add($argument) }
$child = [Diagnostics.Process]::Start($info)
$stdout = $child.StandardOutput.ReadToEndAsync(); $stderr = $child.StandardError.ReadToEndAsync()
try {
    if (-not $child.WaitForExit(75000)) {
        $child.Kill($true)
        if (-not $child.WaitForExit(5000)) { throw 'Owned OLE App exit unconfirmed' }
        throw 'Owned OLE App deadline exceeded'
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $OutputRoot -File -Filter '*.jsonl')) {
        Write-Host ('KUAIYI_OLE_LOG_BEGIN ' + $file.Name)
        Get-Content -LiteralPath $file.FullName | ForEach-Object { Write-Host $_ }
        Write-Host ('KUAIYI_OLE_LOG_END ' + $file.Name)
    }
    if (-not (Test-Path -LiteralPath $receipt)) { throw 'Missing actual OLE receipt' }
    $result = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
    Write-Host 'KUAIYI_OLE_REPORT_BEGIN'; Write-Host ($result | ConvertTo-Json -Depth 12 -Compress); Write-Host 'KUAIYI_OLE_REPORT_END'
    if ($result.Kind -cne 'HostedOleResponsivenessPublicProbe' -or $result.Source -cne 'PublicFixtureOnly' -or $result.ModelCalls -ne 0 -or $result.ProductionTextDrop -isnot [bool] -or -not $result.ProductionTextDrop -or $result.PhysicalMouseAcceptance -cne 'NotRun' -or $result.Windows11Acceptance -cne 'NotRun') { throw 'Invalid OLE evidence boundary' }
    if ($result.IsolationStatus -ceq 'Blocked' -and $child.ExitCode -eq 2) { return $result }
    if ($child.ExitCode -ne 0 -or $result.ExitCode -ne 0 -or $result.IsolationStatus -cne 'Pass' -or $result.AllReceiverCreationsAfterConfirmedExit -isnot [bool] -or -not $result.AllReceiverCreationsAfterConfirmedExit -or -not $result.RecoveredAfterConfirmedExit -or -not $result.CloseConfirmed -or @($result.Scenarios).Count -ne 2 -or @($result.Errors).Count -ne 0) { throw 'Actual OLE isolation/cancel/close/recovery contract failed' }
    foreach ($scenario in $result.Scenarios) {
        if (-not $scenario.realProductionDrop -or -not $scenario.realGetDataBlocked -or -not $scenario.uiHeartbeatAdvanced -or $scenario.registrarThread -eq $scenario.receiverThread -or $scenario.receiverPid -le 0 -or $scenario.sourcePid -le 0) { throw 'Actual OLE scenario not observed' }
    }
    if (@($result.Registrars | Where-Object { -not $_.IsSettledAndRevoked -or $_.FailureCode }).Count -ne 0) { throw 'Actual OLE registration cleanup unconfirmed' }
    return $result
} finally {
    foreach ($drain in @($stdout,$stderr)) {
        if ($drain.IsCompletedSuccessfully) { Write-Host $drain.GetAwaiter().GetResult() }
        else { Write-Host 'Owned OLE output drain incomplete; no unbounded wait.' }
    }
    $child.Dispose()
}
