param([Parameter(Mandatory)][string]$NativeRoot,[Parameter(Mandatory)][string]$OutputRoot)
. "$PSScriptRoot/windows-ci-common.ps1"
function Get-OleResponsivenessWhitelist($result) {
    if ($result.Kind -cne 'HostedOleResponsivenessPublicProbe' -or $result.Source -cne 'PublicFixtureOnly' -or ($result.ModelCalls -isnot [int] -and $result.ModelCalls -isnot [long]) -or $result.ModelCalls -ne 0 -or $result.IsolationStatus -cnotin @('Pass','Fail','Blocked') -or $result.SyntheticPointerStatus -cnotin @('Pass','Blocked','NotRun') -or $result.PhysicalMouseAcceptance -cne 'NotRun' -or $result.Windows11Acceptance -cne 'NotRun') { throw 'Invalid OLE evidence boundary' }
    if (($result.ExitCode -isnot [int] -and $result.ExitCode -isnot [long]) -or $result.ExitCode -cnotin @(0,2,3) -or ($result.IsolationStatus -ceq 'Pass' -and $result.ExitCode -ne 0) -or ($result.IsolationStatus -ceq 'Blocked' -and $result.ExitCode -ne 2) -or ($result.IsolationStatus -ceq 'Fail' -and $result.ExitCode -ne 3)) { throw 'Invalid OLE exit status' }
    foreach ($name in @('ProductionTextDrop','RecoveredAfterConfirmedExit','CloseConfirmed','AllReceiverCreationsAfterConfirmedExit')) { if ($result.$name -isnot [bool]) { throw 'Invalid OLE boolean type' } }
    if (-not $result.ProductionTextDrop -or @($result.Scenarios).Count -gt 2 -or @($result.Errors).Count -gt 64 -or @($result.StartupDiagnostics).Count -gt 8 -or @($result.Registrars).Count -gt 8 -or @($result.ReceiverTransitions).Count -gt 16 -or @($result.ReceiverEvents).Count -gt 4096) { throw 'Invalid OLE finite receipt scope' }
    $scenarios = @()
    foreach ($scenario in @($result.Scenarios)) {
        if ($scenario.name -cnotin @('cancel-recover','close')) { throw 'Invalid OLE scenario name' }
        foreach ($name in @('realProductionDrop','realGetDataBlocked','uiHeartbeatAdvanced')) { if ($scenario.$name -isnot [bool]) { throw 'Invalid OLE scenario boolean' } }
        foreach ($name in @('registrarThread','receiverThread','receiverPid','sourcePid')) { if (($scenario.$name -isnot [int] -and $scenario.$name -isnot [long]) -or $scenario.$name -le 0 -or $scenario.$name -gt [uint32]::MaxValue) { throw 'Invalid actual OLE identity evidence' } }
        if ($scenario.registrarThread -eq $scenario.receiverThread -or $scenario.sourcePid -eq $scenario.receiverPid) { throw 'Invalid OLE independent identity evidence' }
        $scenarios += [pscustomobject]@{ name = $scenario.name; realProductionDrop = $scenario.realProductionDrop; realGetDataBlocked = $scenario.realGetDataBlocked; uiHeartbeatAdvanced = $scenario.uiHeartbeatAdvanced }
    }
    if (@($scenarios | ForEach-Object { $_.name } | Group-Object | Where-Object Count -gt 1).Count -ne 0) { throw 'Duplicate actual OLE scenario' }
    $diagnostics = @()
    $stages = @('Entry','Apartment','Dispatcher','HelloRead','HelloValidated','BackendCreate','ParentVerify','ActivationOpen','NativeExportCreate','ExportValidate','ExportQueued','Controls','ParentStart','ParentHelloWrite','ParentHelloRead','ParentExportRead','ParentRegister','ParentReady','ParentRun','ParentShutdown')
    $codes = @('None','Argument','InvalidOperation','InvalidData','Io','FileMissing','DirectoryMissing','Unauthorized','DllMissing','BadImage','Com','Cancelled','Disposed','Timeout','Other')
    foreach ($item in @($result.StartupDiagnostics)) {
        foreach ($name in @('OriginalRunSettled','OriginalControlSettled','OwnedFinalized')) { if ($item.$name -isnot [bool]) { throw 'Invalid original OLE task observation' } }
        if ($item.DiagnosticReadSettled -isnot [bool] -or $item.DiagnosticReadFailure -cnotin $codes -or ($null -ne $item.OwnedExitCode -and (($item.OwnedExitCode -isnot [int] -and $item.OwnedExitCode -isnot [long]) -or $item.OwnedExitCode -lt [int]::MinValue -or $item.OwnedExitCode -gt [int]::MaxValue))) { throw 'Invalid OLE original diagnostic settlement' }
        $failure = $null
        if ($null -ne $item.StartupFailure) {
            $f = $item.StartupFailure
            if ($f.Stage -cnotin $stages -or $f.Exception -cnotin $codes -or ($f.HResult -isnot [int] -and $f.HResult -isnot [long]) -or $f.HResult -lt [int]::MinValue -or $f.HResult -gt [int]::MaxValue) { throw 'Invalid OLE fixed startup failure' }
            $failure = [pscustomobject]@{ Stage = $f.Stage; Exception = $f.Exception; HResult = $f.HResult }
        }
        $diagnostics += [pscustomobject]@{ OwnedExitCode = $item.OwnedExitCode; StartupFailure = $failure; DiagnosticReadSettled = $item.DiagnosticReadSettled; DiagnosticReadFailure = $item.DiagnosticReadFailure; OriginalRunSettled = $item.OriginalRunSettled; OriginalControlSettled = $item.OriginalControlSettled; OwnedFinalized = $item.OwnedFinalized }
    }
    if ($result.IsolationStatus -ceq 'Pass') {
        if (-not $result.AllReceiverCreationsAfterConfirmedExit -or -not $result.RecoveredAfterConfirmedExit -or -not $result.CloseConfirmed -or @($scenarios).Count -ne 2 -or @($result.Errors).Count -ne 0 -or @($result.StartupDiagnostics).Count -lt 3 -or @($result.StartupDiagnostics).Count -ne @($result.Registrars).Count) { throw 'Actual OLE isolation pass unconfirmed' }
        foreach ($scenario in $scenarios) { if (-not $scenario.realProductionDrop -or -not $scenario.realGetDataBlocked -or -not $scenario.uiHeartbeatAdvanced) { throw 'Actual OLE scenario pass unconfirmed' } }
        foreach ($item in $diagnostics) { if (($null -ne $item.OwnedExitCode -and $item.OwnedExitCode -notin @(0,-1)) -or -not $item.OriginalRunSettled -or -not $item.OriginalControlSettled -or -not $item.OwnedFinalized -or -not $item.DiagnosticReadSettled -or $item.DiagnosticReadFailure -cne 'None' -or ($null -ne $item.StartupFailure -and ($item.StartupFailure.Stage -cne 'ParentRun' -or ($item.StartupFailure.Exception -cne 'Cancelled' -and ($item.StartupFailure.Exception -cne 'Io' -or $item.StartupFailure.HResult -ne -2147024858))))) { throw 'Actual OLE diagnostic settlement unconfirmed' } }
        foreach ($registrar in @($result.Registrars)) { if ($registrar.IsSettledAndRevoked -isnot [bool] -or -not $registrar.IsSettledAndRevoked -or $null -ne $registrar.FailureCode) { throw 'Actual OLE registration cleanup unconfirmed' } }
        $created = @($result.ReceiverTransitions | Where-Object { $_.phase -ceq 'Created' })
        $ready = @($result.ReceiverTransitions | Where-Object { $_.phase -ceq 'Ready' })
        if ($created.Count -ne @($result.StartupDiagnostics).Count -or $ready.Count -lt 3 -or $ready.Count -gt $created.Count) { throw 'Actual receiver creation history unconfirmed' }
        for ($i=0; $i -lt $created.Count; $i++) {
            $creation=$created[$i];$parsed=[guid]::Empty
            if (-not [guid]::TryParse([string]$creation.Instance,[ref]$parsed) -or $parsed -eq [guid]::Empty -or @($creation.prior).Count -ne $i -or @($created | Where-Object { $_.Instance -ceq $creation.Instance }).Count -ne 1 -or @($result.StartupDiagnostics | Where-Object { $_.Instance -ceq $creation.Instance }).Count -ne 1 -or @($result.Registrars | Where-Object { $_.Instance -ceq $creation.Instance }).Count -ne 1) { throw 'Invalid original receiver instance association' }
            if ($creation.safe -isnot [bool] -or -not $creation.safe) { throw 'Actual receiver creation gate unconfirmed' }
            foreach ($prior in @($creation.prior)) {
                $earlier=@($created | Select-Object -First $i | Where-Object { $_.Instance -ceq $prior.Instance })
                $owner=@($result.StartupDiagnostics | Where-Object { $_.Instance -ceq $prior.Instance })
                if ($earlier.Count -ne 1 -or $owner.Count -ne 1 -or $prior.pid -ne $owner[0].OwnedProcessId -or @($creation.prior | Where-Object { $_.Instance -ceq $prior.Instance }).Count -ne 1) { throw 'Prior original receiver identity unconfirmed' }
                foreach ($name in @('actualExit','runSettled','controlSettled','ownedExit','revoked')) { if ($prior.$name -isnot [bool] -or -not $prior.$name) { throw 'Prior original receiver settlement unconfirmed' } } }
        }
        foreach ($binding in $ready) {
            foreach ($name in @('pid','registrarThread')) { if (($binding.$name -isnot [int] -and $binding.$name -isnot [long]) -or $binding.$name -le 0 -or $binding.$name -gt [uint32]::MaxValue) { throw 'Invalid ready receiver identity' } }
            $owned=@($result.StartupDiagnostics | Where-Object { $_.Instance -ceq $binding.Instance -and $_.OwnedProcessId -eq $binding.pid -and $null -ne $_.OwnedExitCode })
            $registrar=@($result.Registrars | Where-Object { $_.Instance -ceq $binding.Instance -and $_.NativeRegistrationThreadId -eq $binding.registrarThread })
            if (@($ready | Where-Object { $_.pid -eq $binding.pid }).Count -ne 1 -or @($created | Where-Object { $_.Instance -ceq $binding.Instance }).Count -ne 1 -or $owned.Count -ne 1 -or $registrar.Count -ne 1) { throw 'Actual original receiver binding unconfirmed' }
        }
        foreach ($scenario in @($result.Scenarios)) {
            $binding=@($ready | Where-Object { $_.pid -eq $scenario.receiverPid -and $_.registrarThread -eq $scenario.registrarThread })
            if ($binding.Count -ne 1) { throw 'Actual scenario receiver binding unconfirmed' }
            $drops=@($result.ReceiverEvents | Where-Object { $_.Instance -ceq $binding[0].Instance -and $_.Phase -ceq 'DropBegin' })
            if ($drops.Count -ne 1 -or $drops[0].SourcePid -ne $scenario.sourcePid -or $drops[0].NativeThreadId -ne $scenario.receiverThread -or $drops[0].Apartment -cne 'STA') { throw 'Actual Drop source/STA binding unconfirmed' }
        }
        if ($result.Scenarios[0].receiverPid -eq $result.Scenarios[1].receiverPid) { throw 'Actual close/recovery receiver identity reused' }
    }
    $knownReasons=@{
        'MouseOrModifierAlreadyHeld'='MouseOrModifierAlreadyHeld';'OwnedSourceNotForeground'='OwnedSourceNotForeground';'PresentationDidNotCaptureSyntheticPress'='PresentationDidNotCaptureSyntheticPress';'SendInputRejected'='SendInputRejected';'OwnedWindowNotAtInputPoint'='OwnedWindowNotAtInputPoint';'InputOutsideVirtualDesktop'='InputOutsideVirtualDesktop'
        'TimeoutException:ReceiverReadyDeadline'='ReceiverReadyDeadline';'TimeoutException:RealDragEnterNotObserved'='RealDragEnterNotObserved';'TimeoutException:OriginalReceiverSettlementDeadline'='OriginalReceiverSettlementDeadline'
        'InvalidOperationException:Real production Drop/source/apartment not observed.'='DropSourceStaUnconfirmed';'InvalidOperationException:Original GetData/Drop no longer live before response action.'='OriginalGetDataNoLongerLive';'InvalidOperationException:Presentation heartbeat stopped during real GetData.'='HeartbeatStoppedDuringGetData'
        'InvalidOperationException:Close ownership unconfirmed.'='CloseOwnershipUnconfirmed';'InvalidOperationException:Cancel ownership unconfirmed.'='CancelOwnershipUnconfirmed';'InvalidOperationException:Recovery cleanup unconfirmed.'='RecoveryOwnershipUnconfirmed';'InvalidOperationException:Old receiver reused.'='OldReceiverReused'
    }
    $reasons=@($result.Errors | ForEach-Object { if ($_ -is [string] -and $knownReasons.ContainsKey($_)) { $knownReasons[$_] } else { 'Other' } })
    return [pscustomobject]@{
        Kind = 'HostedOleResponsivenessPublicProbe'; Source = 'PublicFixtureOnly'; ModelCalls = 0; ProductionTextDrop = $result.ProductionTextDrop
        IsolationStatus = $result.IsolationStatus; SyntheticPointerStatus = $result.SyntheticPointerStatus
        PhysicalMouseAcceptance = 'NotRun'; Windows11Acceptance = 'NotRun'
        RecoveredAfterConfirmedExit = $result.RecoveredAfterConfirmedExit; CloseConfirmed = $result.CloseConfirmed
        AllReceiverCreationsAfterConfirmedExit = $result.AllReceiverCreationsAfterConfirmedExit
        Scenarios = $scenarios; StartupDiagnostics = $diagnostics; ErrorsCount = @($result.Errors).Count; Errors = $reasons
        ExitCode = $result.ExitCode
    }
}

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
if (-not ('KuaiYiCiDiscard' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Threading.Tasks;
public static class KuaiYiCiDiscard {
    public static async Task<bool> Drain(Stream stream) {
        byte[] buffer = new byte[4096];
        try { while (await stream.ReadAsync(buffer, 0, buffer.Length) != 0) Array.Clear(buffer, 0, buffer.Length); return true; }
        catch { return false; }
        finally { Array.Clear(buffer, 0, buffer.Length); }
    }
}
'@
}
$heldApp = Get-Variable -Name UnconfirmedOleApp -Scope Script -ErrorAction SilentlyContinue
if ($null -ne $heldApp -and $null -ne $heldApp.Value) { throw 'Prior owned OLE App settlement unconfirmed' }
$child = [Diagnostics.Process]::Start($info)
$stdout = [KuaiYiCiDiscard]::Drain($child.StandardOutput.BaseStream); $stderr = [KuaiYiCiDiscard]::Drain($child.StandardError.BaseStream)
try {
    if (-not $child.WaitForExit(75000)) {
        $child.Kill($true)
        if (-not $child.WaitForExit(5000)) { throw 'Owned OLE App exit unconfirmed' }
        throw 'Owned OLE App deadline exceeded'
    }
    if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr),1000) -or -not $stdout.IsCompletedSuccessfully -or -not $stderr.IsCompletedSuccessfully -or -not $stdout.Result -or -not $stderr.Result) { throw 'Owned OLE output drain unconfirmed' }
    if (-not (Test-Path -LiteralPath $receipt) -or (Get-Item -LiteralPath $receipt).Length -gt 1048576) { throw 'Missing or oversized owned OLE receipt' }
    try { $result = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json; $projected = Get-OleResponsivenessWhitelist $result }
    catch { throw 'Owned OLE receipt validation failed' }
    Write-Host 'KUAIYI_OLE_REPORT_BEGIN'; Write-Host ($projected | ConvertTo-Json -Depth 8 -Compress); Write-Host 'KUAIYI_OLE_REPORT_END'
    if ($result.IsolationStatus -ceq 'Blocked' -and $child.ExitCode -eq 2 -and $result.ExitCode -eq 2) { return $projected }
    if ($child.ExitCode -ne 0 -or $result.ExitCode -ne 0 -or $result.IsolationStatus -cne 'Pass') { throw 'Actual OLE isolation/cancel/close/recovery contract failed' }
    return $projected
} finally {
    if ($child.HasExited -and $stdout.IsCompleted -and $stderr.IsCompleted) { $child.Dispose() }
    else { $script:UnconfirmedOleApp = $child; $script:UnconfirmedOleDrains = @($stdout,$stderr); throw 'Owned OLE App and original reads retained; settlement unconfirmed' }
}
