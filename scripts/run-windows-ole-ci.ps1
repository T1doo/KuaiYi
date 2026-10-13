param([Parameter(Mandatory)][string]$NativeRoot,[Parameter(Mandatory)][string]$OutputRoot)
. "$PSScriptRoot/windows-ci-common.ps1"
. "$PSScriptRoot/ole-ci-diagnostics.ps1"
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
        foreach ($name in @('OriginalRunSettled','OriginalControlSettled','OwnedFinalized','WasEverReady','RegistrationAttempted','ControlAttempted','DeadlineObserved','RevocationRequested','RevokeRunPending','RevokeFailureAbsent','RevokeStopFresh')) { if ($item.$name -isnot [bool]) { throw 'Invalid original OLE task observation' } }
        if ($item.OriginalRunState -cnotin @('UNKNOWN','PENDING','RAN_TO_COMPLETION','FAULTED','CANCELED') -or $item.RevocationReason -cnotin @('UNKNOWN','Closing','Off','Suspended','LeaseInvalidated')) { throw 'Invalid OLE cancellation observation' }

        if ($item.DiagnosticReadSettled -isnot [bool] -or $item.DiagnosticReadFailure -cnotin $codes -or ($null -ne $item.OwnedExitCode -and (($item.OwnedExitCode -isnot [int] -and $item.OwnedExitCode -isnot [long]) -or $item.OwnedExitCode -lt [int]::MinValue -or $item.OwnedExitCode -gt [int]::MaxValue))) { throw 'Invalid OLE original diagnostic settlement' }
        $failure = $null
        if ($null -ne $item.StartupFailure) {
            $f = $item.StartupFailure
            if ($f.Stage -cnotin $stages -or $f.Exception -cnotin $codes -or ($f.HResult -isnot [int] -and $f.HResult -isnot [long]) -or $f.HResult -lt [int]::MinValue -or $f.HResult -gt [int]::MaxValue) { throw 'Invalid OLE fixed startup failure' }
            $failure = [pscustomobject]@{ Stage = $f.Stage; Exception = $f.Exception; HResult = $f.HResult }
        }
        $diagnostics += [pscustomobject]@{ OwnedExitCode = $item.OwnedExitCode; StartupFailure = $failure; DiagnosticReadSettled = $item.DiagnosticReadSettled; DiagnosticReadFailure = $item.DiagnosticReadFailure; OriginalRunSettled = $item.OriginalRunSettled; OriginalControlSettled = $item.OriginalControlSettled; OwnedFinalized = $item.OwnedFinalized; Admission = 'UNKNOWN' }
    }
    if ($result.IsolationStatus -ceq 'Pass') {
        if (-not $result.AllReceiverCreationsAfterConfirmedExit -or -not $result.RecoveredAfterConfirmedExit -or -not $result.CloseConfirmed -or @($scenarios).Count -ne 2 -or @($result.Errors).Count -ne 0 -or @($result.StartupDiagnostics).Count -lt 3 -or @($result.StartupDiagnostics).Count -ne @($result.Registrars).Count) { throw 'Actual OLE isolation pass unconfirmed' }
        foreach ($scenario in $scenarios) { if (-not $scenario.realProductionDrop -or -not $scenario.realGetDataBlocked -or -not $scenario.uiHeartbeatAdvanced) { throw 'Actual OLE scenario pass unconfirmed' } }
        foreach ($item in $diagnostics) {
            if (($null -ne $item.OwnedExitCode -and $item.OwnedExitCode -notin @(0,-1)) -or -not $item.OriginalRunSettled -or -not $item.OriginalControlSettled -or -not $item.OwnedFinalized -or -not $item.DiagnosticReadSettled -or $item.DiagnosticReadFailure -cne 'None') { throw 'Actual OLE diagnostic settlement unconfirmed' }
        }
        $ownerIndex=0
        foreach ($owner in @($result.StartupDiagnostics)) {
            $reg=@($result.Registrars | Where-Object { $_.Instance -ceq $owner.Instance })
            if ($reg.Count -ne 1 -or $reg[0].RegistrationAttempted -isnot [bool] -or $reg[0].OriginalTaskState -cnotin @('UNKNOWN','PENDING','RAN_TO_COMPLETION','FAULTED','CANCELED')) { throw 'Actual registrar task association unconfirmed' }
            if ($owner.WasEverReady) {
                $binding=@($result.ReceiverTransitions | Where-Object { $_.phase -ceq 'Ready' -and $_.Instance -ceq $owner.Instance -and $_.pid -eq $owner.OwnedProcessId -and $_.registrarThread -eq $reg[0].NativeRegistrationThreadId })
                if ($binding.Count -ne 1 -or @($result.ReceiverTransitions | Where-Object { $_.phase -ceq 'Ready' -and $_.Instance -ceq $owner.Instance }).Count -ne 1) { throw 'Actual original receiver binding unconfirmed' }
                $f=$owner.StartupFailure
                $terminal=if ($null -eq $f) { $owner.OriginalRunState -ceq 'RAN_TO_COMPLETION' } else { $f.Stage -ceq 'ParentRun' -and (($f.Exception -ceq 'Cancelled' -and $f.HResult -eq -2146233029 -and $owner.OriginalRunState -ceq 'CANCELED') -or ($f.Exception -ceq 'Io' -and $f.HResult -eq -2147024858 -and $owner.OriginalRunState -ceq 'FAULTED')) }
                if (-not $owner.RegistrationAttempted -or -not $reg[0].RegistrationAttempted -or $reg[0].OriginalTaskState -cne 'RAN_TO_COMPLETION' -or $owner.DeadlineObserved -or -not $terminal) { throw 'Actual OLE diagnostic settlement unconfirmed' }
                $diagnostics[$ownerIndex].Admission='Ready'

            } else {
                $f=$owner.StartupFailure
                if (-not $owner.RevocationRequested -or -not $owner.RevokeRunPending -or -not $owner.RevokeFailureAbsent -or -not $owner.RevokeStopFresh -or $owner.RevocationReason -ceq 'UNKNOWN' -or $owner.DeadlineObserved -or $owner.OriginalRunState -cne 'CANCELED' -or $owner.ControlAttempted -or $owner.RegistrationAttempted -or $reg[0].RegistrationAttempted -or $reg[0].OriginalTaskState -cne 'UNKNOWN' -or $reg[0].NativeRegistrationThreadId -ne 0 -or $reg[0].RegistrationThreadId -ne 0 -or ($owner.OwnedProcessId -isnot [int] -and $owner.OwnedProcessId -isnot [long]) -or $owner.OwnedProcessId -le 0 -or $null -eq $owner.OwnedExitCode -or $null -eq $f -or $f.Stage -cnotin @('ParentHelloWrite','ParentHelloRead','ParentExportRead') -or $f.Exception -cne 'Cancelled' -or $f.HResult -ne -2146233029 -or @($result.ReceiverTransitions | Where-Object { $_.phase -ceq 'Ready' -and $_.Instance -ceq $owner.Instance }).Count -ne 0 -or @($result.ReceiverEvents | Where-Object { $_.Instance -ceq $owner.Instance }).Count -ne 0) { throw 'Unready OLE cancellation unconfirmed' }
                $diagnostics[$ownerIndex].Admission='CancelledBeforeRegistration'
            }
            $ownerIndex++
        }

        foreach ($registrar in @($result.Registrars)) { if ($registrar.IsSettledAndRevoked -isnot [bool] -or -not $registrar.IsSettledAndRevoked -or $null -ne $registrar.FailureCode) { throw 'Actual OLE registration cleanup unconfirmed' } }
        $created = @($result.ReceiverTransitions | Where-Object { $_.phase -ceq 'Created' })
        $ready = @($result.ReceiverTransitions | Where-Object { $_.phase -ceq 'Ready' })
        if ($created.Count -ne @($result.StartupDiagnostics).Count -or $ready.Count -lt 3 -or $ready.Count -gt $created.Count) { throw 'Actual receiver creation history unconfirmed' }
        for ($i=0; $i -lt $created.Count; $i++) {
            $creation=$created[$i];$parsed=[guid]::Empty
            if (-not [guid]::TryParse([string]$creation.Instance,[ref]$parsed) -or $parsed -eq [guid]::Empty -or @($creation.prior).Count -ne $i -or @($created | Where-Object { $_.Instance -ceq $creation.Instance }).Count -ne 1 -or @($result.StartupDiagnostics | Where-Object { $_.Instance -ceq $creation.Instance }).Count -ne 1 -or @($result.Registrars | Where-Object { $_.Instance -ceq $creation.Instance }).Count -ne 1) { throw 'Invalid original receiver instance association' }
            if ($creation.safe -isnot [bool] -or -not $creation.safe -or $creation.wanted -isnot [bool] -or -not $creation.wanted -or $creation.leaseCurrent -isnot [bool] -or -not $creation.leaseCurrent) { throw 'Actual receiver creation gate unconfirmed' }
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

function Resolve-OleReceipt($Result,[int]$ChildExit) {
    $projected=Get-OleResponsivenessWhitelist $Result
    if ($ChildExit -ne $projected.ExitCode) { throw 'Actual OLE process/receipt exit mismatch' }
    return $projected # Fail and Blocked are evidence, not exceptions that discard the receipt.
}
function Read-OleContractReceipt([string]$Path,[int]$ChildExit,$Diagnostic,[string]$DiagnosticPath) {
    try {
        $Diagnostic.Stage='ReceiptRead'; Write-OleDiagnostic $DiagnosticPath $Diagnostic
        if (-not (Test-Path -LiteralPath $Path)) { $Diagnostic.ReceiptState='MISSING'; throw 'Missing owned OLE receipt' }
        if ((Get-Item -LiteralPath $Path).Length -gt 1048576) { $Diagnostic.ReceiptState='OVERSIZED'; throw 'Oversized owned OLE receipt' }
        try { $result=Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json } catch { $Diagnostic.ReceiptState='MALFORMED'; throw }
        $Diagnostic.Stage='ReceiptValidate'; Write-OleDiagnostic $DiagnosticPath $Diagnostic
        try { $projected=Resolve-OleReceipt $result $ChildExit } catch {
            $Diagnostic.ReceiptState='REJECTED'
            $rules=@{
                'Actual OLE diagnostic settlement unconfirmed'='TaskSettlement';'Unready OLE cancellation unconfirmed'='UnreadyCancellation';'Actual registrar task association unconfirmed'='RegistrarTask'
                'Actual OLE registration cleanup unconfirmed'='RegistrationCleanup';'Actual receiver creation gate unconfirmed'='CreationGate';'Prior original receiver settlement unconfirmed'='PriorSettlement'
                'Actual original receiver binding unconfirmed'='ReadyBinding';'Actual Drop source/STA binding unconfirmed'='DropBinding';'Invalid original receiver instance association'='InstanceBinding'
            }
            if ($rules.ContainsKey($_.Exception.Message)) { $Diagnostic.ReceiptRule=$rules[$_.Exception.Message] }
            throw
        }
        $Diagnostic.ReceiptState='VALID';$Diagnostic.Stage='ContractResult'
        return $projected
    } catch {
        $Diagnostic.FailureStage=$Diagnostic.Stage;$Diagnostic.FailureCode=Get-OleExceptionCode $_.Exception;$Diagnostic.HResult=$_.Exception.HResult
        throw
    } finally {
        try { Write-OleDiagnostic $DiagnosticPath $Diagnostic }
        catch { $Diagnostic.CleanupCode='DiagnosticWriteFailed';if ($Diagnostic.FailureStage -ceq 'UNKNOWN') { throw } }
    }
}
function Wait-OleApp($Child,$Stdout,$Stderr,$Diagnostic,[string]$DiagnosticPath) {
    $Diagnostic.Stage='AppWait';Write-OleDiagnostic $DiagnosticPath $Diagnostic
    if (-not $Child.WaitForExit(75000)) {
        $Child.Kill($true)
        if (-not $Child.WaitForExit(5000)) { throw [TimeoutException]::new('Owned OLE App exit unconfirmed') }
        throw [TimeoutException]::new('Owned OLE App deadline exceeded')
    }
    $Diagnostic.Stage='OutputDrain';Write-OleDiagnostic $DiagnosticPath $Diagnostic
    if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($Stdout,$Stderr),1000) -or -not $Stdout.IsCompletedSuccessfully -or -not $Stderr.IsCompletedSuccessfully -or -not $Stdout.Result -or -not $Stderr.Result) { throw 'Owned OLE output drain unconfirmed' }
}
$diagnostic=New-OleDiagnostic
[void][IO.Directory]::CreateDirectory($OutputRoot)
$diagnosticPath=Join-Path $OutputRoot 'runner-diagnostic.json'
$child=$null; $stdout=$null; $stderr=$null
function Set-OleStage([string]$Stage) { $diagnostic.Stage=$Stage; Write-OleDiagnostic $diagnosticPath $diagnostic }
function Invoke-OleBuild([string[]]$Arguments) {
    $global:LASTEXITCODE=$null; $diagnostic.CommandExitCode=$null
    try { Invoke-Checked dotnet $Arguments | ForEach-Object { Write-Host $_ } }
    finally { if ($global:LASTEXITCODE -is [int]) { $diagnostic.CommandExitCode=$global:LASTEXITCODE } }
}
try {
# An ordinary build directory, not a publish/bundle/installer. Use only the already restored, locked projects.
$app = Join-Path $OutputRoot 'app'
[void][IO.Directory]::CreateDirectory($app)
Set-OleStage 'RestoreReceiver'
Invoke-OleBuild @('restore','src/KuaiYi.OleReceiver/KuaiYi.OleReceiver.csproj','--locked-mode')
Set-OleStage 'RestoreSource'
Invoke-OleBuild @('restore','tools/KuaiYi.OleFaultSource/KuaiYi.OleFaultSource.csproj','--locked-mode','-p:KuaiYiPublicOleSource=true')
Set-OleStage 'RestoreApp'
Invoke-OleBuild @('restore','src/KuaiYi.App/KuaiYi.App.csproj','--locked-mode')
Set-OleStage 'BuildReceiver'
Invoke-OleBuild @('build','src/KuaiYi.OleReceiver/KuaiYi.OleReceiver.csproj','--configuration','Release','--no-restore')
Set-OleStage 'BuildSource'
Invoke-OleBuild @('build','tools/KuaiYi.OleFaultSource/KuaiYi.OleFaultSource.csproj','--configuration','Release','--no-restore','-p:KuaiYiPublicOleSource=true')
Set-OleStage 'BuildApp'
Invoke-OleBuild @('build','src/KuaiYi.App/KuaiYi.App.csproj','--configuration','Release','--no-restore','--output',$app,'-p:KuaiYiIncludeWindowsCiProbe=true')
Set-OleStage 'StageFiles'
$receiver = Join-Path $script:RepositoryRoot 'src/KuaiYi.OleReceiver/bin/Release/net10.0-windows/win-x64'
foreach ($name in @('KuaiYi.OleReceiver.exe','KuaiYi.OleReceiver.dll','KuaiYi.OleReceiver.runtimeconfig.json','KuaiYi.OleReceiver.deps.json')) { Copy-Item -LiteralPath (Join-Path $receiver $name) -Destination $app }
Copy-Item -LiteralPath (Join-Path $NativeRoot 'KuaiYi.OleNativeBridge/Release/KuaiYi.OleNativeBridge.dll') -Destination $app
$source = Join-Path $script:RepositoryRoot 'tools/KuaiYi.OleFaultSource/bin/Release/net10.0-windows/win-x64/PublicOleFaultSource.exe'
foreach ($extension in @('exe','dll','deps.json','runtimeconfig.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $source -Parent) ('PublicOleFaultSource.'+$extension)) -PathType Leaf)) { throw 'Public OLE source build output missing' }
}
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
Set-OleStage 'AppStart'
$child = [Diagnostics.Process]::Start($info)
$diagnostic.ProcessState='RUNNING'
$stdout = [KuaiYiCiDiscard]::Drain($child.StandardOutput.BaseStream); $stderr = [KuaiYiCiDiscard]::Drain($child.StandardError.BaseStream)
Wait-OleApp $child $stdout $stderr $diagnostic $diagnosticPath
    $projected=Read-OleContractReceipt $receipt $child.ExitCode $diagnostic $diagnosticPath
    return $projected
} catch {
    $diagnostic.FailureStage=$diagnostic.Stage
    $diagnostic.FailureCode=Get-OleExceptionCode $_.Exception
    $diagnostic.HResult=$_.Exception.HResult
    throw
} finally {
    if ($null -ne $child) {
        try {
            $diagnostic.ProcessState=if ($child.HasExited) { 'EXITED' } else { 'RUNNING' }
            if ($child.HasExited) { $diagnostic.ChildExitCode=$child.ExitCode }
        } catch { $diagnostic.CleanupCode=Get-OleExceptionCode $_.Exception }
        $diagnostic.StdoutTask=Get-OleTaskState $stdout
        $diagnostic.StderrTask=Get-OleTaskState $stderr
        if ($diagnostic.ProcessState -ceq 'EXITED' -and $null -ne $stdout -and $null -ne $stderr -and $stdout.IsCompleted -and $stderr.IsCompleted) { $child.Dispose() }
        else { $script:UnconfirmedOleApp=$child; $script:UnconfirmedOleDrains=@($stdout,$stderr); $diagnostic.CleanupCode='InvalidOperation' }
    }
    try { Write-OleDiagnostic $diagnosticPath $diagnostic } catch { # Keep the original failure; absence/rejection is explicitly reported by the outer reader.
    }
}
