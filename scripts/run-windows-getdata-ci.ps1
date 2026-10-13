# Bounded entry for the existing real OLE probe. Raw child output and receipts stay on the ephemeral runner.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/ole-ci-diagnostics.ps1"
function Get-GetDataSummary($Report,[int]$ChildExit,[string]$ExpectedCommit) {
    if ($ExpectedCommit -cnotmatch '^[0-9a-f]{40}$' -or $Report.SourceCommit -cne $ExpectedCommit -or $Report.SourceDirty -isnot [bool] -or $Report.SourceDirty -or $Report.Kind -cne 'HostedWindowsContracts' -or $Report.Scope -cne 'HostedOleFixtureContractsOnly' -or $Report.Windows11Acceptance -cne 'NotRun' -or $Report.E06PhysicalAcceptance -cne 'NotRun') { throw 'Invalid GetData source or scope' }
    if ($Report.ExitCode -isnot [long] -and $Report.ExitCode -isnot [int]) { throw 'Invalid GetData exit type' }
    if ($ChildExit -ne $Report.ExitCode -or $ChildExit -notin @(0,2,3) -or $Report.NativeStatus -cnotin @('Pass','Blocked','Fail') -or $Report.OleStatus -cnotin @('Pass','Blocked','Fail','NotRun')) { throw 'Invalid GetData outcome' }
    $result = [ordered]@{ Kind='HostedGetDataSummary'; SourceCommit=$ExpectedCommit; Status='Fail'; ModelCalls=0; NativeStatus=$Report.NativeStatus; IsolationStatus=$Report.OleStatus; SyntheticPointerStatus='UNKNOWN'; ActualDrop='UNKNOWN'; GetData='UNKNOWN'; CancelRecovery='UNKNOWN'; Close='UNKNOWN'; OriginalTasksSettled='UNKNOWN'; ReceiverCount=$null; OleEvidence=(Get-OleFailureEvidence $Report.OleReport); PhysicalE06='NotRun'; Windows11Acceptance='NotRun'; ExitCode=3 }
    if ($ChildExit -eq 3 -or $Report.NativeStatus -ceq 'Fail' -or $Report.OleStatus -ceq 'Fail') { return [pscustomobject]$result }
    if ($Report.NativeStatus -ceq 'Blocked' -or $Report.OleStatus -ceq 'Blocked') { $result.Status='Blocked'; $result.ExitCode=2; return [pscustomobject]$result }
    foreach ($name in @('NativeDlls','PeChecksPassed','DllLoadsPassed','ExportsPassed')) { if (($Report.$name -isnot [int] -and $Report.$name -isnot [long]) -or $Report.$name -ne 1) { throw 'Native GetData prerequisite unconfirmed' } }
    if ($ChildExit -ne 0 -or $Report.NativeStatus -cne 'Pass' -or $Report.OleStatus -cne 'Pass' -or $Report.CTestsPassed -ne 0 -or $Report.OverlayStatus -cne 'NotRun' -or @($Report.Errors).Count -ne 0) { throw 'GetData pass unconfirmed' }
    $ole=$Report.OleReport
    foreach ($name in @('ModelCalls','ExitCode','ErrorsCount')) { if (($ole.$name -isnot [int] -and $ole.$name -isnot [long]) -or $ole.$name -ne 0) { throw 'Invalid GetData integer evidence' } }
    if ($ole.Kind -cne 'HostedOleResponsivenessPublicProbe' -or $ole.Source -cne 'PublicFixtureOnly' -or $ole.IsolationStatus -cne 'Pass' -or $ole.PhysicalMouseAcceptance -cne 'NotRun' -or $ole.Windows11Acceptance -cne 'NotRun' -or $ole.SyntheticPointerStatus -cnotin @('Pass','Blocked') -or $Report.OleSyntheticPointerStatus -cne $ole.SyntheticPointerStatus -or @($ole.Errors).Count -ne 0) { throw 'Invalid GetData evidence boundary' }
    foreach ($name in @('ProductionTextDrop','RecoveredAfterConfirmedExit','CloseConfirmed','AllReceiverCreationsAfterConfirmedExit')) { if ($ole.$name -isnot [bool] -or -not $ole.$name) { throw 'GetData lifecycle unconfirmed' } }
    if (@($ole.Scenarios).Count -ne 2 -or @($ole.Scenarios | Where-Object name -CEQ 'cancel-recover').Count -ne 1 -or @($ole.Scenarios | Where-Object name -CEQ 'close').Count -ne 1) { throw 'GetData scenarios unconfirmed' }
    foreach ($scenario in $ole.Scenarios) { foreach ($name in @('realProductionDrop','realGetDataBlocked','uiHeartbeatAdvanced')) { if ($scenario.$name -isnot [bool] -or -not $scenario.$name) { throw 'Actual GetData or heartbeat unconfirmed' } } }
    $count=@($ole.StartupDiagnostics).Count
    if ($count -lt 3 -or $count -gt 8) { throw 'GetData original owners unconfirmed' }
    foreach ($owner in $ole.StartupDiagnostics) {
        foreach ($name in @('OriginalRunSettled','OriginalControlSettled','OwnedFinalized','DiagnosticReadSettled')) { if ($owner.$name -isnot [bool] -or -not $owner.$name) { throw 'GetData original task pending' } }
        if ($owner.DiagnosticReadFailure -cne 'None') { throw 'GetData diagnostic read failed' }
        if ($owner.Admission -cnotin @('Ready','CancelledBeforeRegistration')) { throw 'GetData admission unconfirmed' }
    }
    if (@($ole.StartupDiagnostics | Where-Object { $_.Admission -ceq 'Ready' }).Count -lt 3) { throw 'GetData ready owners unconfirmed' }
    $result.Status='Pass'; $result.SyntheticPointerStatus=$ole.SyntheticPointerStatus; $result.ActualDrop='Pass'; $result.GetData='Pass'; $result.CancelRecovery='Pass'; $result.Close='Pass'; $result.OriginalTasksSettled=$true; $result.ReceiverCount=$count; $result.ExitCode=0
    return [pscustomobject]$result
}

$root=Split-Path $PSScriptRoot -Parent
$child=$null; $stdout=$null; $stderr=$null; $output=$null
$observedProcessState='UNKNOWN'; $observedProcessExit=$null
$summary=[pscustomobject]@{Kind='HostedGetDataSummary';Status='Fail';Reason='RunnerFailure';PhysicalE06='NotRun';Windows11Acceptance='NotRun';ExitCode=3}
try {
    if (-not $IsWindows) { $summary.Status='Blocked'; $summary.Reason='WindowsRequired'; $summary.ExitCode=2 }
    else {
        Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Threading.Tasks;
public static class KuaiYiGetDataDiscard {
    public static async Task<bool> Drain(Stream stream) {
        byte[] buffer = new byte[4096];
        try { while (await stream.ReadAsync(buffer, 0, buffer.Length) != 0) Array.Clear(buffer, 0, buffer.Length); return true; }
        catch { return false; }
        finally { Array.Clear(buffer, 0, buffer.Length); }
    }
}
'@
        $expected=(& git -C $root rev-parse HEAD 2>$null).Trim()
        if ($LASTEXITCODE -ne 0 -or $expected -cnotmatch '^[0-9a-f]{40}$') { throw 'Source unavailable' }
        $output=Join-Path $root ('artifacts/windows-ci/' + [guid]::NewGuid().ToString('N'))
        $info=[Diagnostics.ProcessStartInfo]::new((Join-Path $PSHOME 'pwsh.exe'))
        $info.UseShellExecute=$false; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true; $info.WorkingDirectory=$root
        # Prevent the legacy runner from writing its broad summary into the public Actions summary.
        [void]$info.Environment.Remove('GITHUB_STEP_SUMMARY')
        foreach ($argument in @('-NoProfile','-NonInteractive','-File',(Join-Path $PSScriptRoot 'run-windows-ci.ps1'),'-OleOnly','-OutputRoot',$output)) { [void]$info.ArgumentList.Add($argument) }
        $child=[Diagnostics.Process]::Start($info)
        $stdout=[KuaiYiGetDataDiscard]::Drain($child.StandardOutput.BaseStream); $stderr=[KuaiYiGetDataDiscard]::Drain($child.StandardError.BaseStream)
        if (-not $child.WaitForExit(720000)) { $summary.Reason='ChildDeadline'; throw 'Child deadline' }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr),2000) -or -not $stdout.IsCompletedSuccessfully -or -not $stderr.IsCompletedSuccessfully -or -not $stdout.Result -or -not $stderr.Result) { $summary.Reason='OutputDrainUnconfirmed'; throw 'Read settlement' }
        $receipt=Join-Path $output 'report.json'
        if (-not (Test-Path -LiteralPath $receipt) -or (Get-Item -LiteralPath $receipt).Length -gt 1048576) { $summary.Reason='ReceiptUnavailable'; throw 'Receipt unavailable' }
        try { $summary=Get-GetDataSummary (Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json) $child.ExitCode $expected }
        catch { $summary.Reason='ReceiptRejected' }
    }
} catch { # Only fixed summary fields leave this boundary; no exception message or raw output.
} finally {
    if ($null -ne $child) {
        $settled=$false
        try {
            if (-not $child.HasExited) { $child.Kill($true); [void]$child.WaitForExit(5000) }
        } catch { $settled=$false }
        try {
            $observation=Get-OleProcessObservation $child $stdout $stderr
            $observedProcessState=$observation.State; $observedProcessExit=$observation.ExitCode
        } catch { $observedProcessState='UNKNOWN' }
        try {
            if ($null -ne $stdout -and $null -ne $stderr) { [void][Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr),2000) }
            $settled=$child.HasExited -and $null -ne $stdout -and $null -ne $stderr -and $stdout.IsCompletedSuccessfully -and $stderr.IsCompletedSuccessfully -and $stdout.Result -and $stderr.Result
        } catch { $settled=$false }
        if ($settled) { $child.Dispose() }
        else {
            $script:UnsettledGetDataChild=$child; $script:UnsettledGetDataReads=@($stdout,$stderr)
            $summary.Status='Fail'; $summary.ExitCode=3
            $summary | Add-Member -Force CleanupReason 'OriginalChildSettlementUnconfirmed'
        }
    }
}
$processState='UNKNOWN';$processExit=$null
if ($null -ne $child) { # Use the cached observation made before disposal below.
    $processState=$observedProcessState; $processExit=$observedProcessExit
}
$summary | Add-Member RunnerProcess ([pscustomobject]@{State=$processState;ExitCode=$processExit;StdoutTask=(Get-OleTaskState $stdout);StderrTask=(Get-OleTaskState $stderr)})
if ($null -ne $output) { $summary=Add-GetDataDiagnostics $summary $output }
Write-Host ('KUAIYI_GETDATA_SUMMARY ' + ($summary | ConvertTo-Json -Depth 12 -Compress))
exit $summary.ExitCode
