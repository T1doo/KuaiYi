# Shared by the real runner and offline failure-path tests. Never serialize an unvalidated input object.
function New-OleDiagnostic([string]$Boundary='Runner') {
    return [pscustomobject]@{ Version=1; Boundary=$Boundary; Stage='Entry'; FailureStage='UNKNOWN'; FailureCode='UNKNOWN'; HResult=$null; CleanupCode='UNKNOWN'; ProcessState='UNKNOWN'; ChildExitCode=$null; CommandExitCode=$null; StdoutTask='UNKNOWN'; StderrTask='UNKNOWN'; ReceiptState='UNKNOWN'; ReceiptRule='UNKNOWN'; DropObserved='UNKNOWN'; GetDataObserved='UNKNOWN'; CancelConfirmed='UNKNOWN'; RecoveryConfirmed='UNKNOWN'; CloseConfirmed='UNKNOWN'; Trace=@(); Owners=@() }
}
function Get-OleTaskState($Task) {
    if ($null -eq $Task) { return 'UNKNOWN' }
    if (-not $Task.IsCompleted) { return 'PENDING' }
    if ($Task.IsCanceled) { return 'CANCELED' }
    if ($Task.IsFaulted) { return 'FAULTED' }
    return 'RAN_TO_COMPLETION'
}
function Get-OleProcessObservation($Process,$Stdout,$Stderr) {
    $state='UNKNOWN';$code=$null
    if ($null -ne $Process) { try { $state=if ($Process.HasExited) { 'EXITED' } else { 'RUNNING' };if ($Process.HasExited) { $code=$Process.ExitCode } } catch { $state='UNKNOWN' } }
    return [pscustomobject]@{State=$state;ExitCode=$code;StdoutTask=(Get-OleTaskState $Stdout);StderrTask=(Get-OleTaskState $Stderr)}
}
function Get-OleExceptionCode([Exception]$Exception) {
    while ($null -ne $Exception.InnerException) { $Exception=$Exception.InnerException }
    if ($Exception -is [TimeoutException]) { return 'Timeout' }
    if ($Exception -is [OperationCanceledException]) { return 'Cancelled' }
    if ($Exception -is [IO.InvalidDataException]) { return 'InvalidData' }
    if ($Exception -is [IO.IOException]) { return 'Io' }
    if ($Exception -is [UnauthorizedAccessException]) { return 'Unauthorized' }
    if ($Exception -is [InvalidOperationException]) { return 'InvalidOperation' }
    return 'Other'
}
function ConvertTo-OleDiagnostic($Value) {
    $stages=@('UNKNOWN','Entry','Environment','Specification','NativeConfigure','NativeBuild','NativeLoad','DesktopPrerequisites','OleInvoke','RestoreReceiver','RestoreSource','RestoreApp','BuildReceiver','BuildSource','BuildApp','StageFiles','AppStart','AppWait','OutputDrain','ReceiptRead','ReceiptValidate','ContractResult','Complete','Config','WindowCreate','PresentationReady','ReceiverReadyWait','SourceStart','SourceReadyWait','DragEnterWait','GetDataWait','Heartbeat','Cancel','CancelFinalize','Recovery','RecoveryFinalize','Close','CloseFinalize','WorkflowJoin','FinalSnapshot','ReceiptWrite','Cleanup')
    $codes=@('UNKNOWN','None','Timeout','Cancelled','InvalidData','Io','Unauthorized','InvalidOperation','Other','InputBlocked','DiagnosticWriteFailed')
    $tasks=@('UNKNOWN','PENDING','RAN_TO_COMPLETION','FAULTED','CANCELED')
    if ($Value.Version -ne 1 -or $Value.Boundary -cnotin @('Runner','Probe') -or $Value.Stage -cnotin $stages -or $Value.FailureStage -cnotin $stages -or $Value.FailureCode -cnotin $codes -or $Value.CleanupCode -cnotin $codes -or $Value.ProcessState -cnotin @('UNKNOWN','RUNNING','EXITED') -or $Value.StdoutTask -cnotin $tasks -or $Value.StderrTask -cnotin $tasks -or $Value.ReceiptState -cnotin @('UNKNOWN','MISSING','OVERSIZED','MALFORMED','REJECTED','VALID') -or @($Value.Owners).Count -gt 8) { throw 'Invalid safe OLE diagnostic' }
    $rule=if ($Value.PSObject.Properties.Name -contains 'ReceiptRule') { $Value.ReceiptRule } else { 'UNKNOWN' }
    if ($rule -isnot [string] -or $rule -cnotin @('UNKNOWN','TaskSettlement','UnreadyCancellation','RegistrarTask','RegistrationCleanup','CreationGate','PriorSettlement','ReadyBinding','DropBinding','InstanceBinding')) { throw 'Invalid OLE receipt rule' }
    $out=New-OleDiagnostic $Value.Boundary
    $out.ReceiptRule=$rule
    $traceEvents=@('ReceiverReady','ReceiverQueryBegin','ReceiverQueryEndCopy','ReceiverQueryEndNoCopy','ReceiverDropBegin','SourceReady','SourceDragBegin','SourceDragReturn','SourceQuery','SourceGetData','SourceGetDataBlocking','SourceGetDataReturned','OwnedPressDown','OwnedPressUp','OwnedInputCleanupSettled','OwnedInputCleanupFailed','SourceCleanupBegin','SourceCleanupCompleted','SourceCleanupFailed')
    if ($Value.PSObject.Properties.Name -contains 'Trace') {
        if ($Value.Trace -isnot [array] -or $Value.Trace.Count -gt 32) { throw 'Invalid OLE trace bound' }
        foreach ($event in $Value.Trace) { if ($event -isnot [string] -or $event -cnotin $traceEvents) { throw 'Invalid OLE trace event' } }
        if (@($Value.Trace | Select-Object -Unique).Count -ne $Value.Trace.Count) { throw 'Duplicate OLE trace event' }
        $out.Trace=@($Value.Trace)
    }
    foreach ($key in @('Stage','FailureStage','FailureCode','CleanupCode','ProcessState','StdoutTask','StderrTask','ReceiptState')) { $out.$key=$Value.$key }
    foreach ($key in @('HResult','ChildExitCode','CommandExitCode')) {
        $n=$Value.$key
        if ($null -ne $n -and (($n -isnot [int] -and $n -isnot [long]) -or $n -lt [int]::MinValue -or $n -gt [int]::MaxValue)) { throw 'Invalid safe OLE numeric code' }
        $out.$key=$n
    }
    foreach ($key in @('DropObserved','GetDataObserved','CancelConfirmed','RecoveryConfirmed','CloseConfirmed')) {
        if ($Value.$key -cnotin @('UNKNOWN','OBSERVED')) { throw 'Invalid OLE observation' }; $out.$key=$Value.$key
    }
    $out.Owners=@(foreach ($owner in @($Value.Owners)) {
        if ($owner.Kind -cnotin @('Receiver','Source') -or $owner.ProcessState -cnotin @('UNKNOWN','RUNNING','EXITED')) { throw 'Invalid OLE owner' }
        $row=[ordered]@{Kind=$owner.Kind;ProcessState=$owner.ProcessState;ExitCode=$null}
        if ($null -ne $owner.ExitCode -and (($owner.ExitCode -isnot [int] -and $owner.ExitCode -isnot [long]) -or $owner.ExitCode -lt [int]::MinValue -or $owner.ExitCode -gt [int]::MaxValue)) { throw 'Invalid OLE owner exit' }
        $row.ExitCode=$owner.ExitCode
        $row.StartupFailure=$null
        if ($owner.PSObject.Properties.Name -contains 'StartupFailure' -and $null -ne $owner.StartupFailure) {
            $f=$owner.StartupFailure
            $startupStages=@('Entry','Apartment','Dispatcher','HelloRead','HelloValidated','BackendCreate','ParentVerify','ActivationOpen','NativeExportCreate','ExportValidate','ExportQueued','Controls','ParentStart','ParentHelloWrite','ParentHelloRead','ParentExportRead','ParentRegister','ParentReady','ParentRun','ParentShutdown')
            $startupCodes=@('None','Argument','InvalidOperation','InvalidData','Io','FileMissing','DirectoryMissing','Unauthorized','DllMissing','BadImage','Com','Cancelled','Disposed','Timeout','Other')
            if ($f.Stage -cnotin $startupStages -or $f.Exception -cnotin $startupCodes -or ($f.HResult -isnot [int] -and $f.HResult -isnot [long]) -or $f.HResult -lt [int]::MinValue -or $f.HResult -gt [int]::MaxValue) { throw 'Invalid checkpoint startup failure' }
            $row.StartupFailure=[pscustomobject]@{Stage=$f.Stage;Exception=$f.Exception;HResult=$f.HResult}
        }
        foreach ($key in @('Run','Control','MetadataRead','ErrorRead','CloseWrite','CloseFlush','ExitWait')) { if ($owner.$key -cnotin $tasks) { throw 'Invalid original OLE task state' };$row[$key]=$owner.$key }
        $targetEvents=@('ControlEnabled','ControlDisabled','ControlRejected','CallbackEnter','CallbackDrop','CallbackRejected','EnterDisabled','EnterBusy','EnterOutside','EnterNoCopy','EnterSourceRejected','QueryQueued','QueryWritten','QueryRead','QueryStaleEpoch','QueryAccepted','DropQueued','DropWritten','DropRead','DropStaleEpoch','DropAccepted','ExtractionBegin','GetDataInvoking','ExtractionReturned')
        $row.TargetTrace=@()
        if ($owner.PSObject.Properties.Name -contains 'TargetTrace') {
            if ($owner.TargetTrace -isnot [array] -or $owner.TargetTrace.Count -gt 32) { throw 'Invalid OLE target trace bound' }
            foreach ($event in $owner.TargetTrace) { if ($event -isnot [string] -or $event -cnotin $targetEvents) { throw 'Invalid OLE target observation' } }
            if (@($owner.TargetTrace | Select-Object -Unique).Count -ne $owner.TargetTrace.Count) { throw 'Duplicate OLE target observation' }
            $row.TargetTrace=@($owner.TargetTrace)
        }
        foreach ($key in @('RegistrarTask','RegistrarStage','RegistrarFailureStage')) {
            $n=if ($owner.PSObject.Properties.Name -contains $key) { $owner.$key } else { 'UNKNOWN' }
            $allowed=if ($key -ceq 'RegistrarTask') { $tasks } else { @('UNKNOWN','Apartment','Import','Register','Pump','Revoke','Release','Uninitialize','Complete') }
            if ($n -isnot [string] -or $n -cnotin $allowed) { throw 'Invalid OLE registrar observation' };$row[$key]=$n
        }
        [pscustomobject]$row
    })
    return $out
}
function Write-OleDiagnostic([string]$Path,$Value) {
    $safe=ConvertTo-OleDiagnostic $Value
    $json=$safe | ConvertTo-Json -Depth 8 -Compress
    if ([Text.Encoding]::UTF8.GetByteCount($json) -gt 32768) { throw 'Safe OLE diagnostic bound' }
    [IO.File]::WriteAllText($Path+'.tmp',$json)
    [IO.File]::Move($Path+'.tmp',$Path,$true)
}
function Read-OleDiagnostic([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    if ((Get-Item -LiteralPath $Path).Length -gt 32768) { throw 'Safe OLE diagnostic bound' }
    return ConvertTo-OleDiagnostic (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
}
function Get-OleFailureEvidence($Value) {
    if ($null -eq $Value) { return $null }
    $knownReasons=@('MouseOrModifierAlreadyHeld','OwnedSourceNotForeground','PresentationDidNotCaptureSyntheticPress','SendInputRejected','OwnedWindowNotAtInputPoint','InputOutsideVirtualDesktop','ReceiverReadyDeadline','RealDragEnterNotObserved','OriginalReceiverSettlementDeadline','DropSourceStaUnconfirmed','OriginalGetDataNoLongerLive','HeartbeatStoppedDuringGetData','CloseOwnershipUnconfirmed','CancelOwnershipUnconfirmed','RecoveryOwnershipUnconfirmed','OldReceiverReused','Other')
    $stages=@('Entry','Apartment','Dispatcher','HelloRead','HelloValidated','BackendCreate','ParentVerify','ActivationOpen','NativeExportCreate','ExportValidate','ExportQueued','Controls','ParentStart','ParentHelloWrite','ParentHelloRead','ParentExportRead','ParentRegister','ParentReady','ParentRun','ParentShutdown')
    $codes=@('None','Argument','InvalidOperation','InvalidData','Io','FileMissing','DirectoryMissing','Unauthorized','DllMissing','BadImage','Com','Cancelled','Disposed','Timeout','Other')
    if ($Value.Kind -cne 'HostedOleResponsivenessPublicProbe' -or $Value.Source -cne 'PublicFixtureOnly' -or $Value.IsolationStatus -cnotin @('Pass','Fail','Blocked') -or @($Value.Scenarios).Count -gt 2 -or @($Value.StartupDiagnostics).Count -gt 8 -or @($Value.Errors).Count -gt 64) { throw 'Invalid OLE failure evidence' }
    $reasons=@(foreach ($reason in $Value.Errors) { if ($reason -cnotin $knownReasons) { throw 'Invalid safe OLE reason' }; $reason })
    $owners=@(foreach ($item in $Value.StartupDiagnostics) {
        $owner=[ordered]@{}
        foreach ($name in @('OriginalRunSettled','OriginalControlSettled','OwnedFinalized','DiagnosticReadSettled')) { if ($item.$name -isnot [bool]) { throw 'Invalid OLE settlement evidence' };$owner[$name]=$item.$name }
        if ($item.DiagnosticReadFailure -cnotin $codes) { throw 'Invalid OLE diagnostic code' };$owner.DiagnosticReadFailure=$item.DiagnosticReadFailure
        if ($null -ne $item.OwnedExitCode -and (($item.OwnedExitCode -isnot [int] -and $item.OwnedExitCode -isnot [long]) -or $item.OwnedExitCode -lt [int]::MinValue -or $item.OwnedExitCode -gt [int]::MaxValue)) { throw 'Invalid OLE observed exit' }
        $owner.OwnedExitCode=$item.OwnedExitCode;$owner.StartupFailure=$null
        if ($null -ne $item.StartupFailure) {
            $f=$item.StartupFailure
            if ($f.Stage -cnotin $stages -or $f.Exception -cnotin $codes -or ($f.HResult -isnot [int] -and $f.HResult -isnot [long]) -or $f.HResult -lt [int]::MinValue -or $f.HResult -gt [int]::MaxValue) { throw 'Invalid OLE startup evidence' }
            $owner.StartupFailure=[pscustomobject]@{Stage=$f.Stage;Exception=$f.Exception;HResult=$f.HResult}
        }
        [pscustomobject]$owner
    })
    return [pscustomobject]@{IsolationStatus=$Value.IsolationStatus;CompletedScenarios=@($Value.Scenarios).Count;Reasons=$reasons;StartupDiagnostics=$owners}
}
function Add-GetDataDiagnostics($Summary,[string]$OutputRoot) {
    foreach ($entry in @(@('ParentDiagnostic','parent-diagnostic.json'),@('OleDiagnostic','ole-ci/runner-diagnostic.json'),@('ProbeDiagnostic','ole-ci/ole-report.json.diagnostic.json'))) {
        $state='UNKNOWN';$value=$null
        try { $value=Read-OleDiagnostic (Join-Path $OutputRoot $entry[1]);if ($null -ne $value) { $state='CHECKPOINT' } }
        catch { $state='REJECTED' }
        $Summary | Add-Member -Force -NotePropertyName $entry[0] -NotePropertyValue ([pscustomobject]@{State=$state;Value=$value})
    }
    # A checkpoint can establish observations, never overall acceptance or final settlement.
    if ($Summary.ProbeDiagnostic.State -ceq 'CHECKPOINT') {
        $p=$Summary.ProbeDiagnostic.Value
        foreach ($pair in @(@('ActualDrop','DropObserved'),@('GetData','GetDataObserved'),@('CancelRecovery','RecoveryConfirmed'),@('Close','CloseConfirmed'))) {
            if ($Summary.PSObject.Properties.Name -contains $pair[0] -and $Summary.($pair[0]) -ceq 'UNKNOWN' -and $p.($pair[1]) -ceq 'OBSERVED') { $Summary.($pair[0])='OBSERVED' }
        }
    }
    return $Summary
}
