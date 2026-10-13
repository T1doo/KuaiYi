# Exercise the same receipt resolution and summary functions used by the real Windows runner.
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/getdata-ci.test.ps1"
$r=$valid | ConvertTo-Json -Depth 12 | ConvertFrom-Json
$r.ExitCode=3;$r.OleStatus='Fail';$r.OleReport.IsolationStatus='Fail'
$r.OleReport.ExitCode=3;$r.OleReport.Scenarios=@();$r.OleReport.StartupDiagnostics=@()
$x=Get-GetDataSummary $r 3 $commit
if ($x.GetData -cne 'UNKNOWN' -or $x.ActualDrop -cne 'UNKNOWN' -or $x.OriginalTasksSettled -cne 'UNKNOWN') { throw 'Failure defaults falsely imply execution/task observations' }
Write-Host 'PASS failure summary preserves UNKNOWN instead of fabricated negative observations'
$outerTemplate=$valid
# Reuse the actual full receiver fixture and parser; the production resolver is loaded from its AST, never duplicated.
. "$PSScriptRoot/ole-responsiveness-ci.test.ps1"
$rawTemplate=$valid
$tokens=$null;$parseErrors=$null
$helperAst=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'scripts/run-windows-ole-ci.ps1'),[ref]$tokens,[ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw 'OLE helper parse failed' }
foreach ($name in @('Resolve-OleReceipt','Read-OleContractReceipt','Wait-OleApp')) {
    $fn=$helperAst.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$true)
    Invoke-Expression $fn.Extent.Text
}
$checks=1
function Case([string]$name,[scriptblock]$body) { & $body;$script:checks++;Write-Host "PASS $name" }
$temp=Join-Path ([IO.Path]::GetTempPath()) ('kuaiyi-failure-contracts-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
try {
    $receipt=Join-Path $temp 'receipt.json';$diagnosticPath=Join-Path $temp 'diagnostic.json'
    $failed=$rawTemplate | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $failed.IsolationStatus='Fail';$failed.ExitCode=3;$failed.Errors=@('TimeoutException:ReceiverReadyDeadline');$failed.Scenarios=@()
    $failed.StartupDiagnostics[0].StartupFailure.Stage='ParentExportRead'
    $failed.StartupDiagnostics[0].StartupFailure.Exception='Io'
    $failed.StartupDiagnostics[0].StartupFailure.HResult=-2147024858
    $failed | Add-Member RawStderr 'PRIVATE_SENTINEL'
    $failed | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $receipt
    Case 'valid failed receipt survives actual file parser caller assignment and outer early return' {
        $d=New-OleDiagnostic
        $r=$outerTemplate | ConvertTo-Json -Depth 12 | ConvertFrom-Json
        $r.OleReport=& { Read-OleContractReceipt $receipt 3 $d $diagnosticPath }
        $r.OleStatus=$r.OleReport.IsolationStatus;$r.ExitCode=$r.OleReport.ExitCode
        $x=Get-GetDataSummary $r 3 $commit
        $persisted=Read-OleDiagnostic $diagnosticPath
        if ($x.Status -cne 'Fail' -or $x.OleEvidence.Reasons[0] -cne 'ReceiverReadyDeadline' -or $x.OleEvidence.StartupDiagnostics[0].StartupFailure.Stage -cne 'ParentExportRead' -or $x.OleEvidence.CompletedScenarios -ne 0 -or $x.GetData -cne 'UNKNOWN' -or $persisted.ReceiptState -cne 'VALID') { throw 'Failure evidence lost in real parser chain' }
        if (($x | ConvertTo-Json -Depth 12) -match 'PRIVATE_SENTINEL|RawStderr|receiverPid') { throw 'Unsafe evidence escaped' }
    }
    foreach ($mode in @('MISSING','OVERSIZED','MALFORMED','REJECTED','ExitMismatch')) {
        Case "actual receipt early failure $mode keeps stage and receipt state" {
            if (Test-Path $receipt) { Remove-Item -LiteralPath $receipt }
            switch ($mode) {
                OVERSIZED { [IO.File]::WriteAllBytes($receipt,[byte[]]::new(1048577)) }
                MALFORMED { [IO.File]::WriteAllText($receipt,'{PRIVATE_SENTINEL') }
                REJECTED { $bad=$failed | ConvertTo-Json -Depth 12 | ConvertFrom-Json;$bad.Kind='PRIVATE_SENTINEL';$bad | ConvertTo-Json -Depth 12 | Set-Content $receipt }
                ExitMismatch { $failed | ConvertTo-Json -Depth 12 | Set-Content $receipt }
            }
            $d=New-OleDiagnostic;$threw=$false
            try { $null=Read-OleContractReceipt $receipt $(if ($mode -ceq 'ExitMismatch') {0} else {3}) $d $diagnosticPath } catch { $threw=$true }
            $saved=Read-OleDiagnostic $diagnosticPath
            $expectedState=if ($mode -ceq 'ExitMismatch') {'REJECTED'} else {$mode}
            $expectedStage=if ($expectedState -ceq 'REJECTED') {'ReceiptValidate'} else {'ReceiptRead'}
            if (-not $threw -or $saved.ReceiptState -cne $expectedState -or $saved.FailureStage -cne $expectedStage -or $saved.FailureCode -ceq 'UNKNOWN' -or ($saved | ConvertTo-Json -Depth 8) -match 'PRIVATE_SENTINEL') { throw 'Early failure lost safe evidence' }
        }
    }
    # Execute the unmodified helper entry with a test-only command boundary. No dotnet/build/Windows call occurs.
    $fixture=Join-Path $temp 'helper';[void][IO.Directory]::CreateDirectory($fixture)
    Copy-Item (Join-Path $root 'scripts/run-windows-ole-ci.ps1') $fixture
    Copy-Item (Join-Path $root 'scripts/ole-ci-diagnostics.ps1') $fixture
    @'
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$script:RepositoryRoot=$PSScriptRoot
function Invoke-Checked([string]$Command,[string[]]$Arguments) {
    $global:LASTEXITCODE=0
    if ($diagnostic.Stage -cin @('RestoreSource','BuildSource') -and $Arguments -cnotcontains '-p:KuaiYiPublicOleSource=true') { throw 'Source identity configuration missing' }
    if ($diagnostic.Stage -ceq $env:KUAIYI_SYNTHETIC_FAIL_STAGE) { $global:LASTEXITCODE=42;throw 'PRIVATE_SENTINEL' }
}
'@ | Set-Content (Join-Path $fixture 'windows-ci-common.ps1')
    foreach ($stage in @('RestoreReceiver','RestoreSource','RestoreApp','BuildReceiver','BuildSource','BuildApp','StageFiles','AppStart')) {
        Case "real helper failure at $stage preserves exact stage and command exit" {
            $env:KUAIYI_SYNTHETIC_FAIL_STAGE=$stage
            if ($stage -ceq 'AppStart') {
                $receiver=Join-Path $fixture 'src/KuaiYi.OleReceiver/bin/Release/net10.0-windows/win-x64'
                [void][IO.Directory]::CreateDirectory($receiver)
                foreach ($name in @('KuaiYi.OleReceiver.exe','KuaiYi.OleReceiver.dll','KuaiYi.OleReceiver.runtimeconfig.json','KuaiYi.OleReceiver.deps.json')) { [IO.File]::WriteAllText((Join-Path $receiver $name),'synthetic staging placeholder') }
                $sourceOutput=Join-Path $fixture 'tools/KuaiYi.OleFaultSource/bin/Release/net10.0-windows/win-x64'
                [void][IO.Directory]::CreateDirectory($sourceOutput)
                foreach ($extension in @('exe','dll','deps.json','runtimeconfig.json')) { [IO.File]::WriteAllText((Join-Path $sourceOutput ('PublicOleFaultSource.'+$extension)),'synthetic staging placeholder') }
                $native=Join-Path $temp 'KuaiYi.OleNativeBridge/Release';[void][IO.Directory]::CreateDirectory($native)
                [IO.File]::WriteAllText((Join-Path $native 'KuaiYi.OleNativeBridge.dll'),'synthetic staging placeholder')
            }
            $out=Join-Path $temp $stage;$threw=$false
            try { $null=& (Join-Path $fixture 'run-windows-ole-ci.ps1') -NativeRoot $temp -OutputRoot $out } catch { $threw=$true }
            $d=Read-OleDiagnostic (Join-Path $out 'runner-diagnostic.json')
            $expectedExit=if ($stage -cin @('StageFiles','AppStart')) {0} else {42}
            if (-not $threw -or $d.FailureStage -cne $stage -or $d.CommandExitCode -ne $expectedExit -or $d.ChildExitCode -ne $null -or $d.ProcessState -cne 'UNKNOWN' -or $d.StdoutTask -cne 'UNKNOWN') { throw 'Real helper stage lost' }
            if (($d | ConvertTo-Json -Depth 8) -match 'PRIVATE_SENTINEL') { throw 'Raw command failure escaped' }
        }
    }
    Case 'actual helper source output resolves to the external public executable' {
        $script:RepositoryRoot=$root
        $assignment=$helperAst.Find({param($n) $n -is [Management.Automation.Language.AssignmentStatementAst] -and $n.Left.Extent.Text -ceq '$source'},$true)
        $source=Invoke-Expression $assignment.Right.Extent.Text
        if ([IO.Path]::GetFileName($source) -cne 'PublicOleFaultSource.exe') { throw 'Helper still launches a self-excluded source' }
    }
    Add-Type -TypeDefinition @'
public sealed class SyntheticOleProcess {
    public bool Exits = true;
    public bool KillFails = false;
    public bool Killed = false;
    public bool WaitForExit(int milliseconds) { return Exits; }
    public void Kill(bool tree) { if (KillFails) throw new System.InvalidOperationException("PRIVATE_SENTINEL"); Killed=true; }
}
'@
    foreach ($mode in @('Deadline','KillFailure','FaultedRead','CanceledRead','PendingRead','DiscardFailure')) {
        Case "production app wait failure $mode preserves stage and original task state" {
            $child=[SyntheticOleProcess]::new();$d=New-OleDiagnostic
            $pending=[Threading.Tasks.TaskCompletionSource[bool]]::new()
            $stdout=[Threading.Tasks.Task]::FromResult($true);$stderr=[Threading.Tasks.Task]::FromResult($true)
            switch ($mode) {
                Deadline { $child.Exits=$false }
                KillFailure { $child.Exits=$false;$child.KillFails=$true }
                FaultedRead { $stdout=[Threading.Tasks.Task]::FromException[bool]([Exception]::new('PRIVATE_SENTINEL')) }
                CanceledRead { $stdout=[Threading.Tasks.Task]::FromCanceled[bool]([Threading.CancellationToken]::new($true)) }
                PendingRead { $stdout=$pending.Task }
                DiscardFailure { $stdout=[Threading.Tasks.Task]::FromResult($false) }
            }
            $threw=$false
            try { Wait-OleApp $child $stdout $stderr $d $diagnosticPath } catch { $threw=$true }
            $saved=Read-OleDiagnostic $diagnosticPath
            $stage=if ($mode -cin @('Deadline','KillFailure')) {'AppWait'} else {'OutputDrain'}
            if (-not $threw -or $saved.Stage -cne $stage) { throw 'App wait failure lost phase' }
            if ($mode -ceq 'PendingRead' -and $stdout.IsCompleted) { throw 'Timed wait settled original task' }
            $pending.TrySetResult($true) | Out-Null
            if ($stdout.IsFaulted) { $null=$stdout.Exception }
        }
    }
    Case 'actual task states preserve fault cancellation pending completion and unknown independently of process exit' {
        $pending=[Threading.Tasks.TaskCompletionSource[bool]]::new()
        $fault=[Threading.Tasks.Task]::FromException([Exception]::new('PRIVATE_SENTINEL'))
        $cancel=[Threading.Tasks.Task]::FromCanceled([Threading.CancellationToken]::new($true))
        if ((Get-OleTaskState $null) -cne 'UNKNOWN' -or (Get-OleTaskState $pending.Task) -cne 'PENDING' -or (Get-OleTaskState $fault) -cne 'FAULTED' -or (Get-OleTaskState $cancel) -cne 'CANCELED' -or (Get-OleTaskState ([Threading.Tasks.Task]::CompletedTask)) -cne 'RAN_TO_COMPLETION') { throw 'Original task states conflated' }
        $x=Get-OleProcessObservation ([pscustomobject]@{HasExited=$true;ExitCode=17}) $fault $pending.Task
        if ($x.State -cne 'EXITED' -or $x.ExitCode -ne 17 -or $x.StdoutTask -cne 'FAULTED' -or $x.StderrTask -cne 'PENDING') { throw 'Read fault erased real exit' }
        $pending.SetResult($true);$null=$fault.Exception
    }
    Add-Type -Path (Join-Path $root 'tools/KuaiYi.OleCi/OleCiDiagnostic.cs') -CompilerOptions '/nullable:enable'
    Case 'actual CSharp checkpoint survives absent final receipt through production reader and final summary' {
        $out=Join-Path $temp 'checkpoint';[void][IO.Directory]::CreateDirectory((Join-Path $out 'ole-ci'))
        $path=Join-Path $out 'ole-ci/ole-report.json.diagnostic.json'
        $d=[KuaiYi.App.OleCiDiagnostic]::new($path)
        $d.At('GetDataWait');$d.Observed('Drop');$d.Observed('GetData')
        $d.At('CancelFinalize');$d.Fail([TimeoutException]::new('PRIVATE_SENTINEL'))
        $d.At('Cleanup');$d.Fail([IO.IOException]::new('PRIVATE_SENTINEL'),$true);$d.Snapshot()
        $summary=[pscustomobject]@{Status='Fail';ActualDrop='UNKNOWN';GetData='UNKNOWN';CancelRecovery='UNKNOWN';Close='UNKNOWN';OriginalTasksSettled='UNKNOWN';ExitCode=3}
        $x=Add-GetDataDiagnostics $summary $out
        if ($x.ProbeDiagnostic.State -cne 'CHECKPOINT' -or $x.ProbeDiagnostic.Value.FailureStage -cne 'CancelFinalize' -or $x.ProbeDiagnostic.Value.FailureCode -cne 'Timeout' -or $x.ProbeDiagnostic.Value.CleanupCode -cne 'Io' -or $x.GetData -cne 'OBSERVED' -or $x.CancelRecovery -cne 'UNKNOWN' -or $x.OriginalTasksSettled -cne 'UNKNOWN' -or ($x | ConvertTo-Json -Depth 12) -match 'PRIVATE_SENTINEL') { throw 'Checkpoint lost first failure or overclaimed settlement' }
        [IO.File]::WriteAllText($path,'{"Stage":"PRIVATE_SENTINEL"}')
        $x=Add-GetDataDiagnostics $summary $out
        if ($x.ProbeDiagnostic.State -cne 'REJECTED' -or $null -ne $x.ProbeDiagnostic.Value) { throw 'Invalid checkpoint accepted' }
    }
    Case 'CSharp originals do not report a swallowed wrapper success as original success' {
        $fault=[Threading.Tasks.Task]::FromException([Exception]::new('PRIVATE_SENTINEL'))
        $cancel=[Threading.Tasks.Task]::FromCanceled([Threading.CancellationToken]::new($true))
        if ([KuaiYi.App.OleCiDiagnostic]::TaskState($fault) -cne 'FAULTED' -or [KuaiYi.App.OleCiDiagnostic]::TaskState($cancel) -cne 'CANCELED' -or [KuaiYi.App.OleCiDiagnostic]::TaskState($null) -cne 'UNKNOWN' -or [KuaiYi.App.OleCiDiagnostic]::TasksState([Threading.Tasks.Task[]]@($fault,[Threading.Tasks.Task]::CompletedTask)) -cne 'FAULTED') { throw 'Original state collapsed' }
        $null=$fault.Exception
    }
    Case 'finite trace deduplicates observations and cleanup retains its first failure' {
        $path=Join-Path $temp 'trace.json';$d=[KuaiYi.App.OleCiDiagnostic]::new($path)
        $d.At('DragEnterWait');$d.Trace([KuaiYi.App.OleCiTraceEvent]::SourceDragBegin);$d.Trace([KuaiYi.App.OleCiTraceEvent]::SourceDragBegin)
        $d.Trace([KuaiYi.App.OleCiTraceEvent]::ReceiverQueryEndNoCopy);$d.Fail([TimeoutException]::new('PRIVATE_SENTINEL'))
        $d.Fail([IO.IOException]::new('PRIVATE_SENTINEL'),$true);$d.Fail([InvalidOperationException]::new('PRIVATE_SENTINEL'),$true)
        $saved=Read-OleDiagnostic $path
        if ($saved.Trace.Count -ne 2 -or $saved.Trace[0] -cne 'SourceDragBegin' -or $saved.Trace[1] -cne 'ReceiverQueryEndNoCopy' -or $saved.FailureStage -cne 'DragEnterWait' -or $saved.FailureCode -cne 'Timeout' -or $saved.CleanupCode -cne 'Io' -or $saved.GetDataObserved -cne 'UNKNOWN') { throw 'Trace or primary/cleanup semantics lost' }
        if (($saved | ConvertTo-Json -Depth 8) -match 'PRIVATE_SENTINEL') { throw 'Private error escaped' }
    }
    foreach ($mode in @('Unknown','NonString','Scalar','Oversized','Duplicate','RegistrarTask','RegistrarStage','RegistrarFailureStage')) {
        Case "diagnostic rejects unsafe trace or registrar observation $mode" {
            $d=New-OleDiagnostic 'Probe'
            switch ($mode) {
                Unknown { $d.Trace=@('PRIVATE_SENTINEL') }
                NonString { $d.Trace=@(17) }
                Scalar { $d.Trace='SourceDragBegin' }
                Oversized { $d.Trace=@('SourceDragBegin') * 33 }
                Duplicate { $d.Trace=@('SourceDragBegin','SourceDragBegin') }
                default {
                    $owner=[KuaiYi.App.OleCiOwnerDiagnostic]::new();$owner.$mode='PRIVATE_SENTINEL'
                    $d.Owners=@($owner | ConvertTo-Json -Depth 8 | ConvertFrom-Json)
                }
            }
            $threw=$false;try { $null=ConvertTo-OleDiagnostic $d } catch { $threw=$true }
            if (-not $threw) { throw 'Unsafe diagnostic accepted' }
        }
    }
    Case 'registrar original pending task is independent of cleanup stage' {
        $d=New-OleDiagnostic 'Probe';$owner=[KuaiYi.App.OleCiOwnerDiagnostic]::new()
        $owner.RegistrarTask='PENDING';$owner.RegistrarStage='Revoke';$owner.RegistrarFailureStage='UNKNOWN'
        $d.Owners=@($owner | ConvertTo-Json -Depth 8 | ConvertFrom-Json)
        $safe=ConvertTo-OleDiagnostic $d
        if ($safe.Owners[0].RegistrarTask -cne 'PENDING' -or $safe.Owners[0].RegistrarStage -cne 'Revoke' -or $safe.CloseConfirmed -cne 'UNKNOWN') { throw 'Registrar observation overclaims settlement' }
    }
    Case 'target callback and transmission observations never promote acceptance' {
        $d=New-OleDiagnostic 'Probe';$owner=[KuaiYi.App.OleCiOwnerDiagnostic]::new()
        $owner.TargetTrace=@('ControlEnabled','CallbackEnter','EnterOutside','QueryWritten','QueryStaleEpoch')
        $d.Owners=@($owner | ConvertTo-Json -Depth 8 | ConvertFrom-Json)
        $safe=ConvertTo-OleDiagnostic $d
        if ($safe.Owners[0].TargetTrace.Count -ne 5 -or $safe.GetDataObserved -cne 'UNKNOWN' -or $safe.DropObserved -cne 'UNKNOWN' -or $safe.CloseConfirmed -cne 'UNKNOWN') { throw 'Target observations promoted acceptance' }
    }
    foreach ($mode in @('Unknown','NonString','Scalar','Oversized','Duplicate')) {
        Case "target observations reject unsafe field $mode" {
            $d=New-OleDiagnostic 'Probe';$owner=[KuaiYi.App.OleCiOwnerDiagnostic]::new() | ConvertTo-Json -Depth 8 | ConvertFrom-Json
            switch ($mode) {
                Unknown { $owner.TargetTrace=@('PRIVATE_SENTINEL') }
                NonString { $owner.TargetTrace=@(17) }
                Scalar { $owner.TargetTrace='CallbackEnter' }
                Oversized { $owner.TargetTrace=@('CallbackEnter') * 33 }
                Duplicate { $owner.TargetTrace=@('CallbackEnter','CallbackEnter') }
            }
            $d.Owners=@($owner);$threw=$false;try { $null=ConvertTo-OleDiagnostic $d } catch { $threw=$true }
            if (-not $threw) { throw 'Unsafe target observation accepted' }
        }
    }
    Write-Host "OLE_FAILURE_PATH_CONTRACTS passed=$checks failed=0; actual parser/checkpoint/helper paths with synthetic failures only; Windows GetData NotRun"
} finally {
    Remove-Item Env:KUAIYI_SYNTHETIC_FAIL_STAGE -ErrorAction SilentlyContinue
    [IO.Directory]::Delete($temp,$true)
}
