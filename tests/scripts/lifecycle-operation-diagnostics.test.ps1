Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/lifecycle-ci-evidence.ps1"
Add-Type -Path "$PSScriptRoot/../../tools/ci/LifecycleOperation.cs"
$checks=0
function Check([bool]$Condition){if(-not $Condition){throw ('Operation diagnostic contract failed '+$script:checks)};$script:checks++}
function Reject([scriptblock]$Operation){$failed=$false;try{& $Operation | Out-Null}catch{$failed=$true};Check $failed}
$private='PRIVATE_SENTINEL_SYNTHETIC_sk path account exception'
# Inject only exceptions and already-observed outcomes into shared pure classification.
# Never call Run, any Windows API, a target process or a model.
foreach($stage in @('CreateJob','VerifyJobInheritance','SetJobLimits','OpenOutput','OpenInput','PrepareProcess','CreateProcess')){
    $r=[KuaiYiLifecycleOperation+Receipt]::new();$r.Stage=[Enum]::Parse([KuaiYiLifecycleOperation+OperationStage],$stage);$r.CleanupConfirmed=$true
    [KuaiYiLifecycleOperation]::RecordFailure($r,[ComponentModel.Win32Exception]::new(5,$private))
    $row=Get-LifecycleOperationEvidence 'SourceHead' $r
    Check ($row.Stage -ceq $stage -and $row.FailureKind -ceq 'Win32' -and $row.ErrorCode -ceq 'AccessDenied')
    Check (-not $row.ProcessCreated -and -not $row.Resumed -and -not $row.RootExitConfirmed -and -not $row.JobEmpty -and -not $row.RescueUsed)
    Check ([KuaiYiLifecycleOperation]::GetReason($r).ToString() -ceq 'OperationFailure')
    Check (($row | ConvertTo-Json -Compress) -notmatch 'PRIVATE_SENTINEL|account|exception|path')
}
foreach($pair in @(@(0,'Zero'),@(2,'FileNotFound'),@(3,'PathNotFound'),@(5,'AccessDenied'),@(6,'InvalidHandle'),@(87,'InvalidParameter'),@(193,'BadImage'),@(203,'EnvVarMissing'),@(267,'DirectoryInvalid'),@(740,'ElevationRequired'),@(2147483647,'Other'),@(-1,'Other'))){
    $r=[KuaiYiLifecycleOperation+Receipt]::new();[KuaiYiLifecycleOperation]::RecordFailure($r,[ComponentModel.Win32Exception]::new($pair[0],$private));Check ($r.Error.ToString() -ceq $pair[1])
}
foreach($pair in @(@([DllNotFoundException]::new($private),'MissingLibrary'),@([EntryPointNotFoundException]::new($private),'MissingEntry'),@([ArgumentException]::new($private),'Argument'),@([IO.IOException]::new($private),'Io'),@([InvalidOperationException]::new($private),'ManagedOther'),@([TimeoutException]::new($private),'Timeout'))){
    $r=[KuaiYiLifecycleOperation+Receipt]::new();[KuaiYiLifecycleOperation]::RecordFailure($r,$pair[0]);Check ($r.Failure.ToString() -ceq $pair[1] -and $r.Error.ToString() -ceq 'None')
}
Check ([KuaiYiLifecycleOperation]::ClassifyWait([uint]::MaxValue,$false).ToString() -ceq 'Failed')
Check ([KuaiYiLifecycleOperation]::ClassifyWait([uint]::MaxValue,$true).ToString() -ceq 'Failed')
Check ([KuaiYiLifecycleOperation]::ClassifyWait(258,$false).ToString() -ceq 'Timeout')
Check ([KuaiYiLifecycleOperation]::ClassifyWait(0,$false).ToString() -ceq 'Completed')
Check ([KuaiYiLifecycleOperation]::ClassifyWait(0,$true).ToString() -ceq 'Timeout')
Check ([KuaiYiLifecycleOperation]::ClassifyWait(128,$true).ToString() -ceq 'Unexpected')
# Expiry before resume remains Timeout, while WAIT_FAILED keeps its Win32 failure.
$r=[KuaiYiLifecycleOperation+Receipt]::new();$r.Stage=[KuaiYiLifecycleOperation+OperationStage]::ResumeThread;$r.ProcessCreated=$true;$r.Status='Timeout';$r.RescueUsed=$true;$r.CleanupConfirmed=$true
[KuaiYiLifecycleOperation]::RecordFailure($r,[TimeoutException]::new($private))
Check ([KuaiYiLifecycleOperation]::GetReason($r).ToString() -ceq 'OperationTimeout' -and -not $r.Resumed)
$r.CleanupConfirmed=$false
Check ([KuaiYiLifecycleOperation]::GetReason($r).ToString() -ceq 'OperationCleanupFailure')
$r.Status='Failure';$r.CleanupConfirmed=$true;$r.Stage=[KuaiYiLifecycleOperation+OperationStage]::WaitRoot
[KuaiYiLifecycleOperation]::RecordFailure($r,[ComponentModel.Win32Exception]::new(6,$private))
Check ([KuaiYiLifecycleOperation]::GetReason($r).ToString() -ceq 'OperationFailure' -and $r.Error.ToString() -ceq 'InvalidHandle')
foreach($missing in @('ProcessCreated','Resumed','RootExitConfirmed','JobEmpty','CleanupConfirmed')){
    $r=[KuaiYiLifecycleOperation+Receipt]::new();$r.Status='Completed';$r.ProcessCreated=$true;$r.Resumed=$true;$r.RootExitConfirmed=$true;$r.JobEmpty=$true;$r.CleanupConfirmed=$true
    Check ([KuaiYiLifecycleOperation]::GetReason($r).ToString() -ceq 'Completed')
    $r.$missing=$false;Check ([KuaiYiLifecycleOperation]::GetReason($r).ToString() -cne 'Completed')
}
$r=[KuaiYiLifecycleOperation+Receipt]::new();$r.Status='Completed';$r.ProcessCreated=$true;$r.Resumed=$true;$r.RootExitConfirmed=$true;$r.JobEmpty=$true;$r.CleanupConfirmed=$true
Check ([KuaiYiLifecycleOperation]::GetReason($r).ToString() -ceq 'Completed')
$r.RescueUsed=$true;Check ([KuaiYiLifecycleOperation]::GetReason($r).ToString() -cne 'Completed')
$r.RescueUsed=$false;$r.Failure=[KuaiYiLifecycleOperation+FailureKind]::Win32;Check ([KuaiYiLifecycleOperation]::GetReason($r).ToString() -cne 'Completed')
foreach($name in @('CreateJobObjectW','SetHandleInformation','GetHandleInformation','SetInformationJobObject','CreateFileW','CreateProcessW','AssignProcessToJobObject','IsProcessInJob','ResumeThread','WaitForSingleObject','GetExitCodeProcess','QueryInformationJobObject','FlushFileBuffers')){
    $method=[KuaiYiLifecycleOperation].GetMethod($name,[Reflection.BindingFlags]'NonPublic,Static')
    $import=@($method.GetCustomAttributes([Runtime.InteropServices.DllImportAttribute],$false))[0]
    Check $import.SetLastError
}
Reject {Get-LifecycleOperationEvidence $private $r}
$r.Stage=[Enum]::ToObject([KuaiYiLifecycleOperation+OperationStage],999);Reject {Get-LifecycleOperationEvidence 'SourceHead' $r}
$r.Stage=[KuaiYiLifecycleOperation+OperationStage]::Start;$r.Status=$private;Reject {Get-LifecycleOperationEvidence 'SourceHead' $r}
$r.Status='Failure';$r.Exit=$private;Reject {Get-LifecycleOperationEvidence 'SourceHead' $r}
$original=Get-Content -LiteralPath "$PSScriptRoot/../../docs/test-evidence/2026-10-13-lifecycle-38443925339.json" -Raw | ConvertFrom-Json
Check ($original.Reason -ceq 'OperationTimeoutOrCleanupFailure' -and $original.Operations[0].Status -ceq 'Failure' -and $original.Runtime -ceq 'NotRun')
# Pure environment projection: no Run/native import. Host cannot turn reusable nodes back on.
$savedReuse=$env:MSBUILDDISABLENODEREUSE
try {
    $env:MSBUILDDISABLENODEREUSE='0'
    $method=[KuaiYiLifecycleOperation].GetMethod('ChildEnvironment',[Reflection.BindingFlags]'NonPublic,Static')
    $block=[string]$method.Invoke($null,@());$entries=@($block.Split([char]0) | Where-Object {$_ -ne ''})
    Check (@($entries | Where-Object {$_ -ceq 'MSBUILDDISABLENODEREUSE=1'}).Count -eq 1)
    Check (@($entries | Where-Object {$_ -ceq 'MSBUILDDISABLENODEREUSE=0'}).Count -eq 0)
    Check ($block.EndsWith(([string][char]0)+[char]0))
    # Exercise the actual installed SDK setter, without creating a BuildManager or running a build.
    $sdk=Join-Path $env:DOTNET_ROOT 'sdk/10.0.401'
    [void][Reflection.Assembly]::LoadFrom((Join-Path $sdk 'Microsoft.Build.Framework.dll'))
    $assembly=[Reflection.Assembly]::LoadFrom((Join-Path $sdk 'Microsoft.Build.dll'))
    $type=$assembly.GetType('Microsoft.Build.Execution.BuildParameters',$true);$property=$type.GetProperty('EnableNodeReuse')
    foreach($value in @('0','1')){
        $env:MSBUILDDISABLENODEREUSE=$value;$parameters=[Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject($type);$property.SetValue($parameters,$true)
        Check ($property.GetValue($parameters) -eq ($value -ceq '0'))
    }
}finally{$env:MSBUILDDISABLENODEREUSE=$savedReuse}
# Actual shared decision helper preserves both operands and stays distinct from final cleanup.
foreach($empty in @($false,$true)){foreach($expired in @($false,$true)){
    $r=[KuaiYiLifecycleOperation+Receipt]::new()
    $timeout=[KuaiYiLifecycleOperation]::CompleteJobWait($r,$empty,$expired)
    Check ($timeout -eq (-not $empty -or $expired))
    $expected=if($empty){'Empty'}else{'NonEmpty'}
    Check ($r.PreRescueJobState.ToString() -ceq $expected -and $r.BudgetExpired -eq $expired)
    $r.JobEmpty=$true;$r.CleanupConfirmed=$true;$r.RescueUsed=$true
    $row=Get-LifecycleOperationEvidence 'NativeConfigure' $r
    Check ($row.PreRescueJobState -ceq $expected -and $row.BudgetExpired -eq $expired -and $row.JobEmpty -and $row.CleanupConfirmed)
}}
$r=[KuaiYiLifecycleOperation+Receipt]::new();$r.JobEmpty=$true;$r.CleanupConfirmed=$true
$row=Get-LifecycleOperationEvidence 'SourceHead' $r
Check ($row.PreRescueJobState -ceq 'Unknown' -and -not $row.BudgetExpired -and $row.JobEmpty)
$r.PreRescueJobState=[Enum]::ToObject([KuaiYiLifecycleOperation+JobState],999)
Reject {Get-LifecycleOperationEvidence 'NativeConfigure' $r}
Write-Host "LIFECYCLE_OPERATION_DIAGNOSTICS passed=$checks failed=0; pure classification/environment/SDK setter; Windows NotRun"
