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
