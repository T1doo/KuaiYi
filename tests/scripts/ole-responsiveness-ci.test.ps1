Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/../..").Path
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'scripts/run-windows-ole-ci.ps1'),[ref]$tokens,[ref]$errors)
if (@($errors).Count -ne 0) { throw 'OLE helper parse failed' }
$fn = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Get-OleResponsivenessWhitelist' },$true)
Invoke-Expression $fn.Extent.Text
$passes=0
function Check([string]$name,[scriptblock]$test) { & $test; $script:passes++; Write-Host "PASS $name" }
function Reject([string]$name,[scriptblock]$mutate) {
    $r=$script:valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    & $mutate $r; $failed=$false
    try { $null=Get-OleResponsivenessWhitelist $r } catch { $failed=$true }
    if (-not $failed) { throw "Expected rejection: $name" }
    $script:passes++; Write-Host "PASS rejects $name"
}
$diags=@(); $regs=@(); $transitions=@(); $prior=@()
for ($i=0;$i -lt 3;$i++) {
    $instance='00000000-0000-0000-0000-00000000000'+($i+1)
    $diags+=@{Instance=$instance;OriginalRunSettled=$true;OriginalRunState='CANCELED';WasEverReady=$true;RegistrationAttempted=$true;ControlAttempted=$true;DeadlineObserved=$false;RevocationRequested=$true;RevokeRunPending=$true;RevokeFailureAbsent=$true;RevokeStopFresh=$true;RevocationReason='Off';OriginalControlSettled=$true;OwnedFinalized=$true;OwnedProcessId=1001+$i;OwnedExitCode=-1;StartupFailure=@{Stage='ParentRun';Exception='Cancelled';HResult=-2146233029};DiagnosticReadSettled=$true;DiagnosticReadFailure='None';Records=@()}
    $regs+=@{Instance=$instance;IsSettledAndRevoked=$true;FailureCode=$null;RegistrationAttempted=$true;OriginalTaskState='RAN_TO_COMPLETION';RegistrationThreadId=401+$i;NativeRegistrationThreadId=201+$i}
    $transitions+=@{phase='Created';instance=$instance;safe=$true;wanted=$true;leaseCurrent=$true;prior=@($prior)}
    $transitions+=@{phase='Ready';Instance=$instance;pid=1001+$i;registrarThread=201+$i}
    $prior+=@{Instance=$instance;pid=1001+$i;actualExit=$true;runSettled=$true;controlSettled=$true;ownedExit=$true;revoked=$true}
}
$valid=@{
    Kind='HostedOleResponsivenessPublicProbe';Source='PublicFixtureOnly';ModelCalls=0;ProductionTextDrop=$true
    IsolationStatus='Pass';SyntheticPointerStatus='Pass';PhysicalMouseAcceptance='NotRun';Windows11Acceptance='NotRun'
    RecoveredAfterConfirmedExit=$true;CloseConfirmed=$true;AllReceiverCreationsAfterConfirmedExit=$true
    Scenarios=@(@{name='cancel-recover';realProductionDrop=$true;realGetDataBlocked=$true;uiHeartbeatAdvanced=$true;receiverPid=1001;sourcePid=2001;registrarThread=201;receiverThread=301},@{name='close';realProductionDrop=$true;realGetDataBlocked=$true;uiHeartbeatAdvanced=$true;receiverPid=1003;sourcePid=2003;registrarThread=203;receiverThread=303})
    StartupDiagnostics=$diags;Registrars=$regs;ReceiverTransitions=$transitions;ReceiverEvents=@(@{Instance='00000000-0000-0000-0000-000000000001';Phase='DropBegin';SourcePid=2001;NativeThreadId=301;Apartment='STA'},@{Instance='00000000-0000-0000-0000-000000000003';Phase='DropBegin';SourcePid=2003;NativeThreadId=303;Apartment='STA'});Errors=@();ExitCode=0
} | ConvertTo-Json -Depth 12 | ConvertFrom-Json
Check 'valid synthetic contract projection' { $x=Get-OleResponsivenessWhitelist $valid; if ($x.IsolationStatus -cne 'Pass' -or @($x.Scenarios).Count -ne 2) { throw 'Projection failed' } }
Check 'private identities metadata and raw errors are excluded' {
    $r=$valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $r | Add-Member RawStderr 'PUBLIC_SENTINEL'
    $r.ReceiverEvents[0] | Add-Member RawValue 'PUBLIC_SENTINEL'; $r.StartupDiagnostics[0].Records=@('PUBLIC_SENTINEL')
    $r.StartupDiagnostics[0].StartupFailure | Add-Member Message 'PUBLIC_SENTINEL'
    $text=Get-OleResponsivenessWhitelist $r | ConvertTo-Json -Depth 8 -Compress
    if ($text -match 'PUBLIC_SENTINEL|1001|2001|receiverPid|registrarThread|RawStderr|Records|Message') { throw 'Raw metadata escaped projection' }
}
Check 'blocked outcome is distinct and contains only finite fields' {
    $r=$valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $r.IsolationStatus='Blocked';$r.ExitCode=2;$r.SyntheticPointerStatus='Blocked';$r.RecoveredAfterConfirmedExit=$false;$r.CloseConfirmed=$false;$r.AllReceiverCreationsAfterConfirmedExit=$false
    $r.Scenarios=@();$r.StartupDiagnostics=@();$r.Registrars=@();$r.ReceiverTransitions=@();$r.Errors=@('PUBLIC_SENTINEL')
    $x=Get-OleResponsivenessWhitelist $r
    if ($x.IsolationStatus -cne 'Blocked' -or $x.ErrorsCount -ne 1 -or (($x | ConvertTo-Json -Depth 8) -match 'PUBLIC_SENTINEL')) { throw 'Invalid blocked projection' }
}
Check 'only existing fixed failure reasons escape blocked receipt' {
    $r=$valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $r.IsolationStatus='Blocked';$r.ExitCode=2;$r.SyntheticPointerStatus='Blocked';$r.Errors=@('TimeoutException:ReceiverReadyDeadline','PUBLIC_SENTINEL')
    $x=Get-OleResponsivenessWhitelist $r
    if ($x.Errors[0] -cne 'ReceiverReadyDeadline' -or $x.Errors[1] -cne 'Other' -or (($x | ConvertTo-Json -Depth 8) -match 'PUBLIC_SENTINEL')) { throw 'Unreviewed failure text escaped' }
}
Reject 'string top boolean'  { param($r) $r.CloseConfirmed='false' }
Reject 'string scenario boolean' { param($r) $r.Scenarios[0].realGetDataBlocked='false' }
Reject 'unknown isolation status' { param($r) $r.IsolationStatus='PUBLIC_SENTINEL' }
Reject 'physical acceptance claim' { param($r) $r.PhysicalMouseAcceptance='Pass' }
Reject 'string exit code' { param($r) $r.ExitCode='0' }
Reject 'raw exit field' { param($r) $r.ExitCode='PUBLIC_SENTINEL' }
Reject 'duplicate scenario' { param($r) $r.Scenarios[1].name='cancel-recover' }
Reject 'unknown scenario' { param($r) $r.Scenarios[1].name='PUBLIC_SENTINEL' }
Reject 'no actual drop' { param($r) $r.Scenarios[0].realProductionDrop=$false }
Reject 'no actual GetData block' { param($r) $r.Scenarios[0].realGetDataBlocked=$false }
Reject 'no independent registrar' { param($r) $r.Scenarios[0].receiverThread=$r.Scenarios[0].registrarThread }
Reject 'same source and receiver' { param($r) $r.Scenarios[0].sourcePid=$r.Scenarios[0].receiverPid }
Reject 'original run still pending' { param($r) $r.StartupDiagnostics[0].OriginalRunSettled=$false }
Reject 'original control still pending' { param($r) $r.StartupDiagnostics[0].OriginalControlSettled=$false }
Reject 'original owner not finalized' { param($r) $r.StartupDiagnostics[0].OwnedFinalized=$false }
Reject 'no original diagnostic settlement' { param($r) $r.StartupDiagnostics[0].DiagnosticReadSettled=$false }
Reject 'ready original run state contradicts cancellation' { param($r) $r.StartupDiagnostics[0].OriginalRunState='FAULTED' }
Reject 'ready registrar original still pending' { param($r) $r.Registrars[0].OriginalTaskState='PENDING' }
Reject 'ready owner deadline fired' { param($r) $r.StartupDiagnostics[0].DeadlineObserved=$true }
Reject 'diagnostic failure' { param($r) $r.StartupDiagnostics[0].DiagnosticReadFailure='Io' }
Reject 'unknown startup failure enum' { param($r) $r.StartupDiagnostics[0].StartupFailure.Stage='PUBLIC_SENTINEL' }
Reject 'unexpected startup EOF' { param($r) $r.StartupDiagnostics[0].StartupFailure.Stage='ParentExportRead';$r.StartupDiagnostics[0].StartupFailure.Exception='Io' }
Reject 'unexpected parent run IO' { param($r) $r.StartupDiagnostics[0].StartupFailure.Exception='Io';$r.StartupDiagnostics[0].StartupFailure.HResult=-2147024891 }
Reject 'missing original exit observation' { param($r) $r.StartupDiagnostics[0].OwnedExitCode=$null }
Reject 'missing registration revocation' { param($r) $r.Registrars[0].IsSettledAndRevoked=$false }
Reject 'unsafe new receiver' { param($r) $r.ReceiverTransitions[2].safe=$false }
Reject 'prior original run still pending' { param($r) $r.ReceiverTransitions[2].prior[0].runSettled=$false }
Reject 'duplicate created instance' { param($r) $r.ReceiverTransitions[2].instance=$r.ReceiverTransitions[0].instance }
Reject 'wrong receiver ready binding' { param($r) $r.ReceiverTransitions[3].Instance=$r.ReceiverTransitions[1].Instance }
Reject 'missing prior lifetime' { param($r) $r.ReceiverTransitions[4].prior=@() }
Reject 'wrong diagnostic owner' { param($r) $r.StartupDiagnostics[0].OwnedProcessId=9999 }
Reject 'wrong prior instance' { param($r) $r.ReceiverTransitions[2].prior[0].Instance='00000000-0000-0000-0000-000000000009' }
Reject 'wrong prior PID' { param($r) $r.ReceiverTransitions[2].prior[0].pid=9999 }
Reject 'wrong Drop source' { param($r) $r.ReceiverEvents[0].SourcePid=9999 }
Reject 'wrong Drop native thread' { param($r) $r.ReceiverEvents[0].NativeThreadId=9999 }
Reject 'wrong Drop apartment' { param($r) $r.ReceiverEvents[0].Apartment='MTA' }
Reject 'spontaneous receiver failure exit' { param($r) $r.StartupDiagnostics[0].OwnedExitCode=3 }
Reject 'wrong scenario receiver' { param($r) $r.Scenarios[0].receiverPid=9999 }
Reject 'raw failure hidden by success' { param($r) $r.Errors=@('PUBLIC_SENTINEL') }
Check 'additional safe original receiver is verified instead of rejected by ordinal' {
    $r=$valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $instance='00000000-0000-0000-0000-000000000004'
    $d=$r.StartupDiagnostics[0] | ConvertTo-Json -Depth 8 | ConvertFrom-Json;$d.Instance=$instance;$d.OwnedProcessId=1004
    $g=$r.Registrars[0] | ConvertTo-Json -Depth 8 | ConvertFrom-Json;$g.Instance=$instance;$g.NativeRegistrationThreadId=204
    $p=@($r.StartupDiagnostics | ForEach-Object { @{Instance=$_.Instance;pid=$_.OwnedProcessId;actualExit=$true;runSettled=$true;controlSettled=$true;ownedExit=$true;revoked=$true} })
    $r.StartupDiagnostics+=@($d);$r.Registrars+=@($g)
    $r.ReceiverTransitions+=@(@{phase='Created';instance=$instance;safe=$true;wanted=$true;leaseCurrent=$true;prior=$p},@{phase='Ready';Instance=$instance;pid=1004;registrarThread=204})
    $r=$r | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $x=Get-OleResponsivenessWhitelist $r
    if ($x.IsolationStatus -cne 'Pass' -or @($x.StartupDiagnostics).Count -ne 4) { throw 'Finite verified receiver rejected' }
}
Check 'synthetic input blocked does not become physical acceptance or erase real isolation' {
    $r=$valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json;$r.SyntheticPointerStatus='Blocked'
    $x=Get-OleResponsivenessWhitelist $r
    if ($x.IsolationStatus -cne 'Pass' -or $x.SyntheticPointerStatus -cne 'Blocked' -or $x.PhysicalMouseAcceptance -cne 'NotRun') { throw 'Evidence scopes conflated' }
}
function Add-UnreadyCancelledReceiver($r) {
    $instance='00000000-0000-0000-0000-000000000004'
    $d=$r.StartupDiagnostics[0] | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $d.Instance=$instance;$d.OwnedProcessId=1004;$d.WasEverReady=$false;$d.RegistrationAttempted=$false;$d.ControlAttempted=$false;$d.StartupFailure.Stage='ParentHelloRead'
    $g=$r.Registrars[0] | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $g.Instance=$instance;$g.RegistrationAttempted=$false;$g.OriginalTaskState='UNKNOWN';$g.NativeRegistrationThreadId=0;$g.RegistrationThreadId=0
    $p=@($r.StartupDiagnostics | ForEach-Object { @{Instance=$_.Instance;pid=$_.OwnedProcessId;actualExit=$true;runSettled=$true;controlSettled=$true;ownedExit=$true;revoked=$true} })
    $r.StartupDiagnostics+=@($d);$r.Registrars+=@($g)
    $r.ReceiverTransitions+=@(@{phase='Created';instance=$instance;safe=$true;wanted=$true;leaseCurrent=$true;prior=$p})
    return $r | ConvertTo-Json -Depth 12 | ConvertFrom-Json
}
Check 'fully settled explicit handshake revocation retains every original owner' {
    $r=Add-UnreadyCancelledReceiver ($valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json)
    $x=Get-OleResponsivenessWhitelist $r
    if ($x.IsolationStatus -cne 'Pass' -or $x.StartupDiagnostics.Count -ne 4 -or $x.StartupDiagnostics[3].Admission -cne 'CancelledBeforeRegistration' -or $x.StartupDiagnostics[0].Admission -cne 'Ready') { throw 'Explicit cancelled owner was lost or confused with ready admission' }
}
foreach ($mode in @('NoRequest','NoReason','AfterRun','AfterFailure','AfterStop','Deadline','RunFaulted','RunPending','OwnerPending','ReadPending','ControlPending','NoExit','ControlStarted','RegisterStarted','RegistrarStarted','RegistrarPending','ThreadStarted','ReadyClaim','EventClaim','Stage','HRESULT','CreatedOff','CreatedStale')) {
    Check "unready cancellation still rejects missing evidence $mode" {
        $r=Add-UnreadyCancelledReceiver ($valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json)
        $d=$r.StartupDiagnostics[3];$g=$r.Registrars[3]
        switch ($mode) {
            NoRequest { $d.RevocationRequested=$false }
            AfterRun { $d.RevokeRunPending=$false }
            AfterFailure { $d.RevokeFailureAbsent=$false }
            AfterStop { $d.RevokeStopFresh=$false }
            NoReason { $d.RevocationReason='UNKNOWN' }
            Deadline { $d.DeadlineObserved=$true }
            RunFaulted { $d.OriginalRunState='FAULTED' }
            RunPending { $d.OriginalRunState='PENDING' }
            OwnerPending { $d.OwnedFinalized=$false }
            ReadPending { $d.DiagnosticReadSettled=$false }
            ControlPending { $d.OriginalControlSettled=$false }
            NoExit { $d.OwnedExitCode=$null }
            ControlStarted { $d.ControlAttempted=$true }
            RegisterStarted { $d.RegistrationAttempted=$true }
            RegistrarStarted { $g.RegistrationAttempted=$true }
            RegistrarPending { $g.OriginalTaskState='PENDING' }
            ThreadStarted { $g.NativeRegistrationThreadId=204 }
            ReadyClaim { $r.ReceiverTransitions+=@([pscustomobject]@{phase='Ready';Instance=$d.Instance;pid=1004;registrarThread=204}) }
            EventClaim { $r.ReceiverEvents+=@([pscustomobject]@{Instance=$d.Instance;Phase='DropBegin';SourcePid=2004;NativeThreadId=304;Apartment='STA'}) }
            Stage { $d.StartupFailure.Stage='ParentRegister' }
            HRESULT { $d.StartupFailure.HResult=-1 }
            CreatedOff { $r.ReceiverTransitions[-1].wanted=$false }
            CreatedStale { $r.ReceiverTransitions[-1].leaseCurrent=$false }
        }
        $failed=$false;try { $null=Get-OleResponsivenessWhitelist $r } catch { $failed=$true }
        if (-not $failed) { throw ('Unproven handshake cancellation accepted: '+$mode) }
    }
}
Check 'claimed ready extra owner requires its own Ready binding' {
    $r=Add-UnreadyCancelledReceiver ($valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json)
    $d=$r.StartupDiagnostics[3];$g=$r.Registrars[3];$d.WasEverReady=$true;$d.RegistrationAttempted=$true;$d.ControlAttempted=$true;$d.StartupFailure.Stage='ParentRun';$g.RegistrationAttempted=$true;$g.OriginalTaskState='RAN_TO_COMPLETION';$g.NativeRegistrationThreadId=204;$g.RegistrationThreadId=404
    $failed=$false;try { $null=Get-OleResponsivenessWhitelist $r } catch { $failed=$true }
    if (-not $failed) { throw 'Extra ready owner without Ready evidence accepted' }
}
Write-Host "OLE_RESPONSIVENESS_SCRIPT_CONTRACTS passed=$passes failed=0; synthetic receipt schema only, Windows GetData NotRun"
