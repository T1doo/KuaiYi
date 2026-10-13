Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/../../scripts/selection-ci-summary.ps1"
$commit='1111111111111111111111111111111111111111'
$kinds=@('Word','Letter','Paragraph','Unicode','Formula','Long') * 3 + @('Paragraphs','Mixed')
$views=@()
foreach ($application in @('Chrome','Edge')) { foreach ($view in @('Html','ReadOnly','Input')) {
    $cases=@();for ($i=0;$i -lt 20;$i++) { $cases += [pscustomobject]@{Index=$i;Kind=$kinds[$i];Outcome='Success';Captured=$true;Matches=$true;ForegroundStable=$true;MarkerStable=$true;BrokerSettled=$true} }
    $views += [pscustomobject]@{Application=$application;Version='1.2.3.4';View=$view;Status='Pass';Reason='Completed';SequenceCompleted=$true;Planned=20;Correct=20;Mismatches=0;BrokerCleanup=$true;ApplicationCleanup=$true;ProfileDeleted=$true;Cases=$cases}
} }
$valid=[pscustomobject]@{Kind='HostedInstalledSelection';SourceCommit=$commit;Scope='ExplicitProductionUiaOnly';Accessibility='DefaultOnDemand';PhysicalSelection='NotRun';Windows11Acceptance='NotRun';ModelCalls=0;Planned=120;Views=$views}
$checks=0
function Copy-Report { return $valid | ConvertTo-Json -Depth 10 | ConvertFrom-Json }
function Reject([string]$Name,[scriptblock]$Mutate) {
    $report=Copy-Report; & $Mutate $report
    $rejected=$false;try { $null=ConvertTo-SelectionSummary $report 0 $commit } catch { $rejected=$true }
    if (-not $rejected) { throw "Boundary accepted $Name" };$script:checks++
}
$pass=ConvertTo-SelectionSummary $valid 0 $commit
if ($pass.Status -cne 'Pass' -or $pass.Correct -ne 120 -or $pass.PhysicalSelection -cne 'NotRun') { throw 'Valid receipt lost' };$checks++
Reject 'wrong source' {param($r) $r.SourceCommit='2222222222222222222222222222222222222222'}
Reject 'physical claim' {param($r) $r.PhysicalSelection='Pass'}
Reject 'reduced denominator' {param($r) $r.Views[0].Planned=19}
Reject 'duplicate view' {param($r) $r.Views[1].View='Html'}
Reject 'forged total' {param($r) $r.Views[0].Correct=19}
Reject 'unknown outcome' {param($r) $r.Views[0].Cases[0].Outcome='PRIVATE_SENTINEL'}
Reject 'string flags' {param($r) $r.Views[0].Cases[0].Matches='true'}
Reject 'foreground mismatch' {param($r) $r.Views[0].Cases[0].ForegroundStable=$false}
Reject 'marker mismatch' {param($r) $r.Views[0].Cases[0].MarkerStable=$false}
Reject 'pending original broker' {param($r) $r.Views[0].Cases[0].BrokerSettled=$false}
Reject 'pending owned job' {param($r) $r.Views[0].ApplicationCleanup=$false}
Reject 'undeleted profile' {param($r) $r.Views[0].ProfileDeleted=$false}
Reject 'wrong case kind' {param($r) $r.Views[0].Cases[0].Kind='Paragraph'}
Reject 'forced accessibility hidden' {param($r) $r.Accessibility='Forced'}
Reject 'model calls' {param($r) $r.ModelCalls=1}
Reject 'string integer' {param($r) $r.Planned='120'}
Reject 'boolean integer' {param($r) $r.ModelCalls=$false}
Reject 'unfinished sequence' {param($r) $r.Views[0].SequenceCompleted=$false}
foreach ($reason in @('ProbeDeadline','FixtureOrSourceFailure','SequenceUnconfirmed')) {
    $infrastructureReason=$reason
    Reject 'infrastructure failure despite 19 matches' {param($r) $v=$r.Views[0];$v.Cases[19].Outcome='EmptyOrUnsupported';$v.Cases[19].Captured=$false;$v.Cases[19].Matches=$false;$v.Correct=19;$v.Reason=$infrastructureReason}
}
$missing=Copy-Report
foreach ($v in $missing.Views) { $v.Status='Blocked';$v.Reason='UnavailableOrUnverified';$v.Version='Unknown';$v.Correct=0;$v.SequenceCompleted=$false;$v.BrokerCleanup=$false;$v.ApplicationCleanup=$false;$v.ProfileDeleted=$false;foreach ($c in $v.Cases) {$c.Outcome='NotRun';$c.Captured=$false;$c.Matches=$false;$c.ForegroundStable=$false;$c.MarkerStable=$false;$c.BrokerSettled=$false} }
if ((ConvertTo-SelectionSummary $missing 2 $commit).Status -cne 'Blocked') {throw 'Missing apps became Pass'};$checks++
$failed=Copy-Report;$failed.Views[0].Cases[0].Matches=$false;$failed.Views[0].Correct=19;$failed.Views[0].Mismatches=1;$failed.Views[0].Status='Fail'
if ((ConvertTo-SelectionSummary $failed 3 $commit).Status -cne 'Fail') {throw 'Wrong text became Pass'};$checks++
$nineteen=Copy-Report;$nineteen.Views[0].Cases[19].Outcome='EmptyOrUnsupported';$nineteen.Views[0].Cases[19].Captured=$false;$nineteen.Views[0].Cases[19].Matches=$false;$nineteen.Views[0].Correct=19
if ((ConvertTo-SelectionSummary $nineteen 0 $commit).Status -cne 'Pass') {throw 'Completed 95 percent capability threshold rejected'};$checks++
$safe=Copy-Report;$safe | Add-Member RawStderr 'PRIVATE_SENTINEL';$safe.Views[0] | Add-Member Path 'PRIVATE_SENTINEL';$safe.Views[0].Cases[0] | Add-Member Text 'PRIVATE_SENTINEL'
if ((ConvertTo-SelectionSummary $safe 0 $commit | ConvertTo-Json -Depth 10) -match 'PRIVATE_SENTINEL|RawStderr') {throw 'Private field escaped'};$checks++
Write-Host "SELECTION_SUMMARY_CONTRACTS passed=$checks failed=0"
$diagnostic=Copy-Report;$diagnostic.Kind='HostedInstalledSelectionDiagnostic';$diagnostic.Scope='DiagnosticOnly';$diagnostic.Planned=6
foreach ($v in $diagnostic.Views) {
    $v.Status='Observed';$v.Planned=1;$v.Correct=0;$v.Cases=@($v.Cases[0]);$v.Cases[0].Outcome='Sensitive';$v.Cases[0].Captured=$false;$v.Cases[0].Matches=$false
    $v.Cases[0] | Add-Member BrokerObservation ([pscustomobject]@{Status='UnsupportedOrProtected';Stage='Root';Exception='Com';HResult=-2147024891})
}
$d=ConvertTo-SelectionSummary $diagnostic 0 $commit -DiagnosticOnly
if ($d.Kind -cne 'HostedSelectionDiagnosticSummary' -or $d.Scope -cne 'DiagnosticOnly' -or $d.Status -cne 'Observed' -or $d.Planned -ne 6 -or $d.Views[0].BrokerObservations[0].Status -cne 'UnsupportedOrProtected') { throw 'Diagnostic scope or status lost' };$checks++
foreach ($mode in @('WrongScope','FakePass','PrivateStage','LateObservation','PendingCleanup','Message')) {
    $r=$diagnostic | ConvertTo-Json -Depth 10 | ConvertFrom-Json;$rejected=$false
    switch ($mode) {
        WrongScope {$r.Scope='ExplicitProductionUiaOnly'}
        FakePass {$r.Views[0].Status='Pass'}
        PrivateStage {$r.Views[0].Cases[0].BrokerObservation.Stage='PRIVATE_SENTINEL'}
        LateObservation {$r.Views[0].Cases[0].Outcome='HardTimeout'}
        PendingCleanup {$r.Views[0].BrokerCleanup=$false}
        Message {$r.Views[0].Cases[0].BrokerObservation | Add-Member Message 'PRIVATE_SENTINEL'}
    }
    try {$result=ConvertTo-SelectionSummary $r 0 $commit -DiagnosticOnly} catch {$rejected=$true}
    if ($mode -ceq 'Message') {if ($rejected -or ($result | ConvertTo-Json -Depth 10) -match 'PRIVATE_SENTINEL|Message') {throw 'Exception message escaped'}}
    elseif (-not $rejected) {throw 'Invalid diagnostic evidence accepted'}
    $checks++
}
Write-Host "SELECTION_SUMMARY_CONTRACTS_TOTAL passed=$checks failed=0"
$reported=$diagnostic | ConvertTo-Json -Depth 10 | ConvertFrom-Json
foreach($v in $reported.Views){$c=$v.Cases[0];$c.Outcome='Crash';$c.BrokerObservation.Status='BrokerFailure';$c.BrokerObservation.Stage='MetadataSerialize';$c.BrokerObservation.Exception='Argument';$c.BrokerObservation.HResult=-2147024809}
if((ConvertTo-SelectionSummary $reported 0 $commit -DiagnosticOnly).Views[0].BrokerObservations[0].Status -cne 'BrokerFailure'){throw 'Reported code failure lost'};$checks++
$reported.Views[0].Cases[0].BrokerObservation.Status='Success';$rejected=$false
try{$null=ConvertTo-SelectionSummary $reported 0 $commit -DiagnosticOnly}catch{$rejected=$true}
if(-not $rejected){throw 'Unknown process crash reused success observation'};$checks++
Write-Host "SELECTION_SUMMARY_FAILURE_CONTRACTS passed=$checks failed=0"

$exited=$diagnostic | ConvertTo-Json -Depth 10 | ConvertFrom-Json
foreach($v in $exited.Views){$c=$v.Cases[0];$c.Outcome='Crash';$c.BrokerObservation=$null;$c | Add-Member BrokerExitCode ([int]-532462766)}
$exitSummary=ConvertTo-SelectionSummary $exited 0 $commit -DiagnosticOnly
if($exitSummary.Views[0].BrokerExitCodes[0] -ne -532462766 -or $exitSummary.Status -cne 'Observed'){throw 'Original exit code lost'};$checks++
foreach($mode in @('String','Boolean','Overflow','Zero','Timeout','ReportedFailure','Unsettled','PrivateField')){
    $r=$exited | ConvertTo-Json -Depth 10 | ConvertFrom-Json;$c=$r.Views[0].Cases[0];$rejected=$false
    switch($mode){
        String {$c.BrokerExitCode='PRIVATE_SENTINEL'}
        Boolean {$c.BrokerExitCode=$true}
        Overflow {$c.BrokerExitCode=2147483648}
        Zero {$c.BrokerExitCode=0}
        Timeout {$c.Outcome='HardTimeout'}
        ReportedFailure {$c.BrokerObservation=[pscustomobject]@{Status='BrokerFailure';Stage='MetadataSerialize';Exception='Argument';HResult=-2147024809}}
        Unsettled {$c.BrokerSettled=$false}
        PrivateField {$c | Add-Member RawStderr 'PRIVATE_SENTINEL'}
    }
    try{$result=ConvertTo-SelectionSummary $r 0 $commit -DiagnosticOnly}catch{$rejected=$true}
    if($mode -ceq 'PrivateField'){if($rejected -or ($result | ConvertTo-Json -Depth 10) -match 'PRIVATE_SENTINEL|RawStderr'){throw 'Exit diagnostics leaked private field'}}
    elseif(-not $rejected){throw 'Invalid original exit evidence accepted'}
    $checks++
}
Write-Host "SELECTION_SUMMARY_EXIT_CONTRACTS passed=$checks failed=0"
