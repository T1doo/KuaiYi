Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/lifecycle-ci-evidence.ps1"
. "$PSScriptRoot/lifecycle-configure-diagnostics.ps1"
Add-Type -Path "$PSScriptRoot/../../tools/ci/LifecycleOperation.cs"
$checks=0
function Check([bool]$Value){if(-not $Value){throw "Configure-only contract failed $script:checks"};$script:checks++}
$repo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=Join-Path $repo ('artifacts/configure-only-tests/'+[guid]::NewGuid().ToString('N'));[void](New-Item -ItemType Directory -Path $output)
$private='PRIVATE_SYNTHETIC_SENTINEL C:\private\account --key=fixture'
$rows=@();foreach($kind in @('CMakeVersion','NativeConfigure','CMakeCache','ConfigureEvents')){$rows+=@{Kind=$kind;ReadStatus='Missing';Truncated=$false;Facts=@{CMakeVersion=$null;ConfigureDone=$false;GenerateDone=$false;CompilerIdSeen=$false;AbiResult='Unknown';WorkingResult='Unknown';VisualStudio2022=$false;TryCompileEvent=$false}}}
$valid=@{DiagnosticStatus='Collected';Files=$rows}|ConvertTo-Json -Depth 8 -Compress
Add-Type -TypeDefinition @'
using System;using System.IO;using System.Collections.Generic;
public static class ConfigureOnlyOperationFixture {
 public static readonly List<string> Events=new List<string>();
 public static object Original,Diagnostic;public static bool ThrowCollector,WriteOutput;
 public static string DiagnosticJson;public static string[] Arguments;
 public static object Run(string exe,string[] args,string directory,string log){
  if(exe=="fixture-cmake"){Events.Add("OriginalReturned");return Original;}
  Events.Add("CollectorInvoked");Arguments=args;
  if(ThrowCollector)throw new IOException("PRIVATE_SYNTHETIC_SENTINEL");
  if(WriteOutput)File.WriteAllText(log,DiagnosticJson);
  return Diagnostic;
 }
}
'@
$errors=$null;$tokens=$null
$runnerPath=Join-Path $PSScriptRoot 'run-configure-only-windows.ps1'
$ast=[Management.Automation.Language.Parser]::ParseFile($runnerPath,[ref]$tokens,[ref]$errors)
Check ($errors.Count -eq 0)
$function=$ast.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Invoke-ConfigureOnlyOperation'},$true)
$source=$function.Extent.Text
Check (([regex]::Matches($source,'\[KuaiYiLifecycleOperation\]::Run\(')).Count -eq 2)
Invoke-Expression ($source.Replace('[KuaiYiLifecycleOperation]::Run(', '[ConfigureOnlyOperationFixture]::Run('))
function Get-Command {param($Name,$CommandType,$ErrorAction) return [pscustomobject]@{Source='fixture-python'}}
function Get-Content {param($LiteralPath,[switch]$Raw) $script:diagnosticReads++;return (Microsoft.PowerShell.Management\Get-Content -LiteralPath $LiteralPath -Raw:$Raw)}
function New-Lease {
    $lease=[pscustomobject]@{SourceProcess=123;Handle=456;Volume=5;Index=6}
    $lease|Add-Member -MemberType ScriptMethod -Name Dispose -Value {$script:leaseCloses++;[ConfigureOnlyOperationFixture]::Events.Add('CloseParentLease');if($script:closeThrows){throw 'PRIVATE_SYNTHETIC_SENTINEL'}}
    return $lease
}
function New-CompletedReceipt {
    $r=[KuaiYiLifecycleOperation+Receipt]::new();$r.Status='Completed';$r.Exit='0';$r.ProcessCreated=$true;$r.Resumed=$true;$r.RootExitConfirmed=$true;$r.JobEmpty=$true;$r.CleanupConfirmed=$true
    return $r
}
foreach($scenario in @('RescuedClean','OriginalUnclean','CollectorUnclean','CollectorTimeoutClean','CollectorExit1','CollectorThrows','SensitiveOutput','OversizedOutput','MissingOutput','DependencyMissing','NoLease','CloseThrows')){
    [ConfigureOnlyOperationFixture]::Events.Clear();[ConfigureOnlyOperationFixture]::Events.Add('LeaseBeforeConfigure')
    $r=New-CompletedReceipt;$r.Status='Timeout';$r.Failure=[KuaiYiLifecycleOperation+FailureKind]::Timeout;$r.Stage=[KuaiYiLifecycleOperation+OperationStage]::WaitJobEmpty;$r.RescueUsed=$true;$r.BudgetExpired=$true;$r.PreRescueJobState=[KuaiYiLifecycleOperation+JobState]::NonEmpty
    if($scenario -ceq 'OriginalUnclean'){$r.Status='CleanupFailure';$r.CleanupConfirmed=$false;$r.JobEmpty=$false}
    $before=Get-LifecycleOperationEvidence 'NativeConfigure' $r|ConvertTo-Json -Compress
    $d=New-CompletedReceipt
    if($scenario -ceq 'CollectorUnclean'){$d.Status='CleanupFailure';$d.CleanupConfirmed=$false;$d.RootExitConfirmed=$false;$d.JobEmpty=$false;$d.RescueUsed=$true}
    if($scenario -ceq 'CollectorTimeoutClean'){$d.Status='Timeout';$d.Failure=[KuaiYiLifecycleOperation+FailureKind]::Timeout;$d.RescueUsed=$true}
    if($scenario -ceq 'CollectorExit1'){$d.Exit='1'}
    [ConfigureOnlyOperationFixture]::Original=$r;[ConfigureOnlyOperationFixture]::Diagnostic=$d
    [ConfigureOnlyOperationFixture]::ThrowCollector=$scenario -ceq 'CollectorThrows';[ConfigureOnlyOperationFixture]::WriteOutput=$scenario -cne 'MissingOutput'
    [ConfigureOnlyOperationFixture]::DiagnosticJson=switch($scenario){SensitiveOutput {$private};OversizedOutput {'x'*8193};default {$valid}}
    $diagnosticPath=Join-Path $output 'ConfigureDiagnostic.local.json';if(Test-Path -LiteralPath $diagnosticPath){Remove-Item -LiteralPath $diagnosticPath}
    $diagnosticReads=0;$leaseCloses=0;$closeThrows=$scenario -ceq 'CloseThrows'
    $summary=[pscustomobject]@{Reason='RunnerFailure';Status='Fail';ExitCode=3;CollectorSettlement='NotStarted'};$operations=[Collections.Generic.List[object]]::new()
    $configureDiagnosticReady=$scenario -cne 'DependencyMissing';$configureLease=if($scenario -cin @('NoLease','DependencyMissing')){$null}else{New-Lease};$configureDiagnostic=$null
    $threw=$false;try{Invoke-ConfigureOnlyOperation 'NativeConfigure' 'fixture-cmake' @()|Out-Null}catch{$threw=$true}
    Check $threw;Check ($summary.ExitCode -eq 3 -and $summary.Status -ceq 'Fail')
    Check ($summary.Reason -ceq $(if($scenario -ceq 'OriginalUnclean'){'OperationCleanupFailure'}else{'OperationTimeout'}))
    Check ((Get-LifecycleOperationEvidence 'NativeConfigure' $r|ConvertTo-Json -Compress) -ceq $before)
    Check ($operations.Count -eq 1 -and $operations[0].PreRescueJobState -ceq 'NonEmpty' -and $operations[0].BudgetExpired)
    Check ($null -eq $configureLease -and $leaseCloses -eq $(if($scenario -cin @('NoLease','DependencyMissing')){0}else{1}))
    $noCollector=$scenario -cin @('OriginalUnclean','DependencyMissing','NoLease')
    Check (([ConfigureOnlyOperationFixture]::Events -contains 'CollectorInvoked') -ne $noCollector)
    $expectedReads=if($scenario -cin @('RescuedClean','SensitiveOutput','CloseThrows')){1}else{0};Check ($diagnosticReads -eq $expectedReads)
    $settlement=if($noCollector){'NotStarted'}elseif($scenario -cin @('CollectorUnclean','CollectorThrows')){'Unknown'}else{'CleanupConfirmed'}
    Check ($summary.CollectorSettlement -ceq $settlement)
    $status=switch($scenario){RescuedClean {'Collected'};OriginalUnclean {'SkippedCleanupUnconfirmed'};DependencyMissing {'ReaderUnavailable'};NoLease {'OwnershipUnconfirmed'};CloseThrows {'LeaseCloseFailed'};default {'ProjectionError'}}
    Check ($configureDiagnostic.DiagnosticStatus -ceq $status)
    if($scenario -cne 'RescuedClean'){Check ($configureDiagnostic.Files.Count -eq 0)}
    Check (($configureDiagnostic|ConvertTo-Json -Depth 8 -Compress) -notmatch 'PRIVATE_SYNTHETIC_SENTINEL|private\\|key=fixture')
    if(-not $noCollector){Check (([ConfigureOnlyOperationFixture]::Arguments -join ',') -ceq ('-I,-B,'+(Join-Path $repo 'tools/ci/lifecycle_configure_collect.py')+',confirmed,123,456,5,6'))}
}
# A diagnostic failure cannot alter successful original completion or its exit value.
foreach($exit in @('0','1','Other')){
    $r=New-CompletedReceipt;$r.Exit=$exit;[ConfigureOnlyOperationFixture]::Original=$r
    $d=New-CompletedReceipt;$d.Status='CleanupFailure';$d.CleanupConfirmed=$false;$d.RootExitConfirmed=$false;$d.JobEmpty=$false;[ConfigureOnlyOperationFixture]::Diagnostic=$d
    [ConfigureOnlyOperationFixture]::ThrowCollector=$false;[ConfigureOnlyOperationFixture]::WriteOutput=$true;[ConfigureOnlyOperationFixture]::DiagnosticJson=$valid
    $before=Get-LifecycleOperationEvidence 'NativeConfigure' $r|ConvertTo-Json -Compress
    $summary=[pscustomobject]@{Reason='Completed';Status='Pass';ExitCode=0;CollectorSettlement='NotStarted'};$operations=[Collections.Generic.List[object]]::new()
    $configureDiagnosticReady=$true;$closeThrows=$false;$leaseCloses=0;$diagnosticReads=0;$configureLease=New-Lease
    $result=Invoke-ConfigureOnlyOperation 'NativeConfigure' 'fixture-cmake' @()
    Check ($result.Exit -ceq $exit -and $summary.ExitCode -eq 0 -and $summary.Status -ceq 'Pass' -and $summary.Reason -ceq 'Completed')
    Check ((Get-LifecycleOperationEvidence 'NativeConfigure' $r|ConvertTo-Json -Compress) -ceq $before)
    Check ($diagnosticReads -eq 0 -and $leaseCloses -eq 1 -and $summary.CollectorSettlement -ceq 'Unknown' -and $configureDiagnostic.Files.Count -eq 0)
}
$runner=[IO.File]::ReadAllText($runnerPath);$old=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'run-lifecycle-windows-ci.ps1'))
$originalConfigure=($old -split '\r?\n'|Where-Object {$_.Contains('$configure=Invoke-BoundedLifecycleOperation')})
Check ($runner.Contains($originalConfigure.Replace('Invoke-BoundedLifecycleOperation','Invoke-ConfigureOnlyOperation')))
Check ($runner.IndexOf('[KuaiYiConfigureRootLease]::Open($output)') -lt $runner.IndexOf("'SourceHead' "+'$git'))
Check ($source.IndexOf('Complete-LifecycleConfigureDiagnostic') -lt $source.IndexOf('$reason='))
Check ($runner -notmatch "Invoke-ConfigureOnlyOperation '(NativeBuild|ManagedRestore|ManagedBuild|LifecycleRuntime)'|--supervise|cmake --build")
$terminal=$runner.Substring($runner.IndexOf("if("+'$configure.Exit'))
Check ($terminal -notmatch 'Invoke-ConfigureOnlyOperation|::Run\(')
if(-not $IsWindows){
    $lines=@(& (Join-Path $PSHOME 'pwsh') -NoProfile -File $runnerPath *>&1);Check ($LASTEXITCODE -eq 2 -and $lines.Count -eq 1)
    $row=([string]$lines[0]).Substring('KUAIYI_CONFIGURE_ONLY_SUMMARY '.Length)|ConvertFrom-Json
    Check ($row.Status -ceq 'Blocked' -and $row.Reason -ceq 'HostedWindowsRequired' -and $row.Operations.Count -eq 0 -and $row.CollectorSettlement -ceq 'NotStarted')
    Check ($row.NativeBuild -ceq 'NotRun' -and $row.ManagedRestore -ceq 'NotRun' -and $row.ManagedBuild -ceq 'NotRun' -and $row.FourCases -ceq 'NotRun' -and $row.ModelCalls -eq 0)
}
Write-Host "CONFIGURE_ONLY_CONTRACTS passed=$checks failed=0; injected operations only; Windows/CMake/FourCases NotRun"
