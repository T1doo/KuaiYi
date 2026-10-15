Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/lifecycle-ci-evidence.ps1"
. "$PSScriptRoot/lifecycle-configure-diagnostics.ps1"
Add-Type -Path "$PSScriptRoot/../../tools/ci/LifecycleOperation.cs"
Add-Type -Path "$PSScriptRoot/../../tools/ci/ConfigureRootLease.cs"
$checks=0
function Check([bool]$Value){if(-not $Value){throw "Configure contract failed $script:checks"};$script:checks++}
function Reject([scriptblock]$Call){$failed=$false;try{& $Call | Out-Null}catch{$failed=$true};Check $failed}
$private='PRIVATE_SENTINEL C:\private\account --key=secret'
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=Join-Path $repo ('artifacts/configure-integration-tests/'+[guid]::NewGuid().ToString('N'));[void](New-Item -ItemType Directory -Path $output)
$valid='{"DiagnosticStatus":"Collected","Files":['
$kinds=@('CMakeVersion','NativeConfigure','CMakeCache','ConfigureEvents');$rows=@()
foreach($kind in $kinds){$rows+=@{Kind=$kind;ReadStatus='Missing';Truncated=$false;Facts=@{CMakeVersion=$null;ConfigureDone=$false;GenerateDone=$false;CompilerIdSeen=$false;AbiResult='Unknown';WorkingResult='Unknown';VisualStudio2022=$false;TryCompileEvent=$false}}}
$valid=(@{DiagnosticStatus='Collected';Files=$rows}|ConvertTo-Json -Depth 8 -Compress)
$parsed=Read-LifecycleConfigureDiagnostic $valid
Check ($parsed.Files.Count -eq 4 -and $parsed.Files[0].ReadStatus -ceq 'Missing')
foreach($key in @('DiagnosticStatus','Kind','ReadStatus','AbiResult','WorkingResult')){
    foreach($shape in @('Array','Object','Number','Null')){
        $value=$valid|ConvertFrom-Json -AsHashtable
        $where=switch($key){DiagnosticStatus {$value};{$_ -in @('Kind','ReadStatus')} {$value.Files[0]};default {$value.Files[0].Facts}}
        $original=$where[$key]
        $where[$key]=switch($shape){Array {,@($original)};Object {@{Value=$original}};Number {1};Null {$null}}
        Reject {Read-LifecycleConfigureDiagnostic ($value|ConvertTo-Json -Depth 8 -Compress)}
    }
}
# Positive facts still survive the strict independent public boundary.
$facts=$valid|ConvertFrom-Json -AsHashtable
$facts.Files[0].ReadStatus='Projected';$facts.Files[0].Facts.CMakeVersion='3.31.6'
$facts.Files[1].ReadStatus='Projected';$facts.Files[1].Facts.ConfigureDone=$true;$facts.Files[1].Facts.GenerateDone=$true;$facts.Files[1].Facts.AbiResult='Done';$facts.Files[1].Facts.WorkingResult='Skipped'
$facts.Files[2].ReadStatus='Projected';$facts.Files[2].Facts.VisualStudio2022=$true
$facts.Files[3].ReadStatus='Projected';$facts.Files[3].Facts.TryCompileEvent=$true
$projected=Read-LifecycleConfigureDiagnostic ($facts|ConvertTo-Json -Depth 8 -Compress)
Check ($projected.Files[0].Facts.CMakeVersion -ceq '3.31.6' -and $projected.Files[1].Facts.GenerateDone -and $projected.Files[2].Facts.VisualStudio2022 -and $projected.Files[3].Facts.TryCompileEvent)
foreach($bad in @($private,('x'*8193),'{"DiagnosticStatus":"SafetyUnknown","DiagnosticStatus":"SafetyUnknown","Files":[]}',($valid.Replace('"CMakeVersion":null','"CMakeVersion":null,"Secret":"PRIVATE_SENTINEL"')),($valid.Replace('"Missing"','"PRIVATE_SENTINEL"')),($valid.Replace('"Truncated":false','"Truncated":1')))){Reject {Read-LifecycleConfigureDiagnostic $bad}}
foreach($change in @('ExtraField','WrongKind','DuplicateKind','ForeignFact','TruncatedFact','UnexpectedFiles','BadVersion','PrivateEnum','DuplicateFact','FalseProjected')){
    $value=$valid|ConvertFrom-Json -AsHashtable
    switch($change){
        ExtraField {$value.Secret=$private}
        WrongKind {$value.Files[0].Kind=$private}
        DuplicateKind {$value.Files[1].Kind='CMakeVersion'}
        ForeignFact {$value.Files[0].ReadStatus='Projected';$value.Files[0].Facts.ConfigureDone=$true}
        TruncatedFact {$value.Files[0].ReadStatus='Truncated';$value.Files[0].Truncated=$true;$value.Files[0].Facts.CMakeVersion='3.31.6'}
        UnexpectedFiles {$value.DiagnosticStatus='SafetyUnknown'}
        BadVersion {$value.Files[0].ReadStatus='Projected';$value.Files[0].Facts.CMakeVersion=$private}
        PrivateEnum {$value.Files[1].Facts.AbiResult=$private}
        DuplicateFact {$duplicate=$valid.Replace('"AbiResult":"Unknown"','"AbiResult":"Unknown","AbiResult":"Unknown"')}
        FalseProjected {$value.Files[0].ReadStatus='Projected'}
    }
    $text=if($change -ceq 'DuplicateFact'){$duplicate}else{$value|ConvertTo-Json -Depth 8 -Compress}
    Reject {Read-LifecycleConfigureDiagnostic $text}
}
# Compile a synthetic operation implementation; invoke the REAL runner function
# AST with only its two native Run call targets replaced by this test fixture.
Add-Type -TypeDefinition @'
using System;using System.IO;using System.Collections.Generic;
public static class ConfigureOperationFixture {
 public static readonly List<string> Events=new List<string>();
 public static object Original,DiagnosticReceipt;public static bool CleanupConfirmed;
 public static string DiagnosticJson;public static bool ThrowCollector;
 public static object Run(string executable,string[] args,string directory,string log){
  if(executable=="fixture-cmake"){
   Events.Add("Failure");Events.Add("Rescue");Events.Add(CleanupConfirmed?"CleanupConfirmed":"CleanupUnconfirmed");return Original;
  }
  Events.Add("Read");if(ThrowCollector)throw new IOException("PRIVATE_SENTINEL");
  File.WriteAllText(log,DiagnosticJson);
  return DiagnosticReceipt;
 }
}
'@
$diagnosticReceipt=[KuaiYiLifecycleOperation+Receipt]::new();$diagnosticReceipt.Status='Completed';$diagnosticReceipt.Exit='0';$diagnosticReceipt.ProcessCreated=$true;$diagnosticReceipt.Resumed=$true;$diagnosticReceipt.RootExitConfirmed=$true;$diagnosticReceipt.JobEmpty=$true;$diagnosticReceipt.CleanupConfirmed=$true
[ConfigureOperationFixture]::DiagnosticReceipt=$diagnosticReceipt
function Get-Command {param($Name,$CommandType,$ErrorAction) return [pscustomobject]@{Source='fixture-python'}}
$errors=$null;$tokens=$null
$ast=[Management.Automation.Language.Parser]::ParseFile("$PSScriptRoot/run-lifecycle-windows-ci.ps1",[ref]$tokens,[ref]$errors)
Check ($errors.Count -eq 0)
$function=$ast.Find({param($node)$node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Invoke-BoundedLifecycleOperation'},$true)
$source=$function.Extent.Text
Check (([regex]::Matches($source,'\[KuaiYiLifecycleOperation\]::Run\(')).Count -eq 2)
Invoke-Expression ($source.Replace('[KuaiYiLifecycleOperation]::Run(', '[ConfigureOperationFixture]::Run('))
function New-Lease {
    $lease=[pscustomobject]@{SourceProcess=123;Handle=456;Volume=5;Index=6}
    $lease|Add-Member -MemberType ScriptMethod -Name Dispose -Value {[ConfigureOperationFixture]::Events.Add('CloseLease');if($script:closeThrows){throw 'PRIVATE_SENTINEL'}}
    return $lease
}
foreach($scenario in @('RescuedClean','Unclean','CollectorThrows','SensitiveOutput','CloseThrows','NoLease','DependencyMissing')){
    [ConfigureOperationFixture]::Events.Clear();[ConfigureOperationFixture]::Events.Add('LeaseBeforeConfigure')
    $clean=$scenario -cne 'Unclean';$closeThrows=$scenario -ceq 'CloseThrows'
    $r=[KuaiYiLifecycleOperation+Receipt]::new();$r.Status=if($clean){'Timeout'}else{'CleanupFailure'};$r.Exit='0';$r.Failure=[KuaiYiLifecycleOperation+FailureKind]::Timeout
    $r.Stage=[KuaiYiLifecycleOperation+OperationStage]::WaitJobEmpty;$r.ProcessCreated=$true;$r.Resumed=$true;$r.RescueUsed=$true;$r.RootExitConfirmed=$true;$r.JobEmpty=$clean;$r.CleanupConfirmed=$clean;$r.BudgetExpired=$true;$r.PreRescueJobState=[KuaiYiLifecycleOperation+JobState]::NonEmpty
    $before=Get-LifecycleOperationEvidence 'NativeConfigure' $r|ConvertTo-Json -Compress
    [ConfigureOperationFixture]::Original=$r;[ConfigureOperationFixture]::CleanupConfirmed=$clean;[ConfigureOperationFixture]::DiagnosticJson=if($scenario -ceq 'SensitiveOutput'){$private}else{$valid};[ConfigureOperationFixture]::ThrowCollector=$scenario -ceq 'CollectorThrows'
    $operations=[Collections.Generic.List[object]]::new();$allLogs=[Collections.Generic.List[string]]::new();$summary=[pscustomobject]@{Reason='RunnerFailure';ExitCode=3;Status='Fail'}
    $configureDiagnosticReady=$scenario -cne 'DependencyMissing'
    $configureLease=if($scenario -cin @('NoLease','DependencyMissing')){$null}else{New-Lease};$configureDiagnostic=$null
    $threw=$false;try {Invoke-BoundedLifecycleOperation 'NativeConfigure' 'fixture-cmake' @() | Out-Null} catch {$threw=$true}
    Check $threw;Check ($summary.ExitCode -eq 3 -and $summary.Status -ceq 'Fail')
    Check ($summary.Reason -ceq $(if($clean){'OperationTimeout'}else{'OperationCleanupFailure'}))
    Check ((Get-LifecycleOperationEvidence 'NativeConfigure' $r|ConvertTo-Json -Compress) -ceq $before)
    Check ($operations.Count -eq 1 -and $operations[0].PreRescueJobState -ceq 'NonEmpty' -and $operations[0].BudgetExpired)
    Check ($null -eq $configureLease)
    $events=[ConfigureOperationFixture]::Events -join ','
    if($scenario -ceq 'Unclean'){
        Check ($events -ceq 'LeaseBeforeConfigure,Failure,Rescue,CleanupUnconfirmed,CloseLease')
        Check ($configureDiagnostic.DiagnosticStatus -ceq 'SkippedCleanupUnconfirmed' -and $allLogs.Count -eq 0)
    } elseif($scenario -cin @('NoLease','DependencyMissing')){
        Check ($events -ceq 'LeaseBeforeConfigure,Failure,Rescue,CleanupConfirmed')
        Check ($configureDiagnostic.DiagnosticStatus -ceq $(if($scenario -ceq 'NoLease'){'OwnershipUnconfirmed'}else{'ReaderUnavailable'}))
    } else {
        Check ($events -ceq 'LeaseBeforeConfigure,Failure,Rescue,CleanupConfirmed,Read,CloseLease')
        $expected=switch($scenario){RescuedClean {'Collected'};CloseThrows {'LeaseCloseFailed'};default {'ProjectionError'}}
        Check ($configureDiagnostic.DiagnosticStatus -ceq $expected -and $allLogs.Count -eq 0)
    }
    Check (($configureDiagnostic|ConvertTo-Json -Depth 8 -Compress) -notmatch 'PRIVATE_SENTINEL|private\\|key=secret')
}
foreach($exit in @('0','1','Other')){
    $r=[KuaiYiLifecycleOperation+Receipt]::new();$r.Status='Completed';$r.Exit=$exit;$r.ProcessCreated=$true;$r.Resumed=$true;$r.RootExitConfirmed=$true;$r.JobEmpty=$true;$r.CleanupConfirmed=$true
    $before=Get-LifecycleOperationEvidence 'NativeConfigure' $r|ConvertTo-Json -Compress
    [ConfigureOperationFixture]::Original=$r;[ConfigureOperationFixture]::CleanupConfirmed=$true;[ConfigureOperationFixture]::ThrowCollector=$true
    $closeThrows=$false;$configureLease=New-Lease;$configureDiagnosticReady=$true;$configureDiagnostic=$null
    $summary=[pscustomobject]@{Reason='Completed';ExitCode=0;Status='Pass'}
    $returned=Invoke-BoundedLifecycleOperation 'NativeConfigure' 'fixture-cmake' @()
    Check ($returned.Exit -ceq $exit -and $summary.ExitCode -eq 0 -and $summary.Status -ceq 'Pass' -and $summary.Reason -ceq 'Completed')
    Check ((Get-LifecycleOperationEvidence 'NativeConfigure' $r|ConvertTo-Json -Compress) -ceq $before)
    Check ($configureDiagnostic.DiagnosticStatus -ceq 'ProjectionError' -and $null -eq $configureLease)
}
# The lease acquisition really precedes every operation in the actual runner AST.
$runner=Get-Content -LiteralPath "$PSScriptRoot/run-lifecycle-windows-ci.ps1" -Raw
Check ($runner.IndexOf('[KuaiYiConfigureRootLease]::Open($output)') -lt $runner.IndexOf("'SourceHead' "+'$git'))
Check ($source.IndexOf('Complete-LifecycleConfigureDiagnostic') -lt $source.IndexOf('$reason='))
Check ($source.IndexOf('::Run($Executable') -lt $source.IndexOf('Complete-LifecycleConfigureDiagnostic'))
# Only compile/reflection: opening any filesystem through Win32 is forbidden here.
$leaseType=[KuaiYiConfigureRootLease];$flags=[Reflection.BindingFlags]'NonPublic,Static'
Check ($null -ne $leaseType.GetMethod('NtCreateFile',$flags))
Check ($null -ne $leaseType.GetMethod('GetFileInformationByHandle',$flags))
foreach($pair in @(@('UnicodeString',16),@('ObjectAttributes',48),@('IoStatus',16),@('FileInformation',52))){
    $type=$leaseType.GetNestedType($pair[0],[Reflection.BindingFlags]::NonPublic)
    $sizeMethod=@([Runtime.InteropServices.Marshal].GetMethods()|Where-Object {$_.Name -ceq 'SizeOf' -and $_.IsGenericMethodDefinition -and $_.GetParameters().Count -eq 0})[0]
    Check ($sizeMethod.MakeGenericMethod($type).Invoke($null,@()) -eq $pair[1])
}
foreach($invalid in @($null,'', $private,'relative','C:\a\..\b',('C:\'+('x'*4097)))){Reject {[KuaiYiConfigureRootLease]::Open($invalid)}}
Write-Host "CONFIGURE_LOCAL_INTEGRATION passed=$checks failed=0; injected operations/file reads only; Windows/CMake/FourCases NotRun"
