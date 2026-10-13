Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/lifecycle-ci-evidence.ps1"
$checks=0
function Check([bool]$Condition){if(-not $Condition){throw ('Lifecycle CI contract failed '+$script:checks)};$script:checks++}
function Reject([scriptblock]$Operation){$failed=$false;try{& $Operation | Out-Null}catch{$failed=$true};Check $failed}
$secret='PRIVATE_SENTINEL_SYNTHETIC_sk'
$cases=@()
for($i=1;$i -le 4;$i++){$cases+= [ordered]@{Case=$i;Status='Pass';Stage=@('FinalCallsReturned','BoundParentTerminationRequested','BeforeBindParentTerminationRequested','WatchdogTerminationIssuedAndSettled')[$i-1];ParentExitConfirmed=$true;WorkerExitConfirmed=$true;ReleaseCallsReturned=($i -eq 1);EofObserved=($i -in @(1,4));RescueUsed=$false;IoSettled=$true}}
function Lines($Rows){return (($Rows | ForEach-Object {'KUAIYI_LIFECYCLE_CASE '+($_ | ConvertTo-Json -Compress)}) -join "`n")}
$valid=Read-LifecyclePublicCases (Lines $cases);Check ($valid.Cases.Count -eq 4 -and -not $valid.Unsettled);foreach($row in $valid.Cases){Check (Test-LifecycleCasePass $row)}
foreach($name in @('ParentExitConfirmed','WorkerExitConfirmed','IoSettled')){$bad=[ordered]@{};foreach($key in $cases[3].Keys){$bad[$key]=$cases[3][$key]};$bad[$name]=$false;Check (-not (Test-LifecycleCasePass ([pscustomobject]$bad)))}
$bad=[ordered]@{};foreach($key in $cases[3].Keys){$bad[$key]=$cases[3][$key]};$bad.RescueUsed=$true;Check (-not (Test-LifecycleCasePass ([pscustomobject]$bad)))
foreach($stage in @('WatchdogNaturalExit','WatchdogKillNotIssued','WatchdogProofMissing')){$bad.Stage=$stage;$bad.RescueUsed=$false;Check (-not (Test-LifecycleCasePass ([pscustomobject]$bad)))}
Reject {Read-LifecyclePublicCases ((Lines $cases)+"`n"+(Lines @($cases[3])))}
Reject {Read-LifecyclePublicCases (Lines @($cases[1]))}
Reject {Read-LifecyclePublicCases ('KUAIYI_LIFECYCLE_CASE {"Case":1,"Case":1}')}
$extra=[ordered]@{};foreach($key in $cases[0].Keys){$extra[$key]=$cases[0][$key]};$extra.Private=$secret;Reject {Read-LifecyclePublicCases (Lines @($extra))}
$extra.Remove('Private');$extra.Stage=$secret;Reject {Read-LifecyclePublicCases (Lines @($extra))}
$extra.Stage='FinalCallsReturned';$extra.IoSettled='true';Reject {Read-LifecyclePublicCases (Lines @($extra))}
$unsettled=Read-LifecyclePublicCases ((Lines $cases)+"`nKUAIYI_LIFECYCLE_UNSETTLED");Check $unsettled.Unsettled
Check ((Read-LifecyclePublicCases "$secret arbitrary text").Cases.Count -eq 0)
Check ((Read-LifecyclePublicCases (Lines @($cases[0]))).Cases.Count -eq 1)
Reject {Read-LifecyclePublicCases ('x'*4194305)}
$faults=@(Get-LifecycleBuildEvidence "C:\$secret\Program.cs(24,8): error CS4014: $secret`nC:\$secret\LifecycleParent.cs(12): warning CS8602: $secret`nprivate.cs(7): error CS9999: $secret`nprivate.cpp(8): error C2440: $secret")
Check ($faults.Count -eq 4)
Check ($faults[1].Code -ceq 'CS4014' -and $faults[1].Source -ceq 'ManagedEntry' -and $faults[1].Line -eq 24)
Check ($faults[3].Code -ceq 'Unknown' -and $faults[3].Source -ceq 'Unknown' -and $null -eq $faults[3].Line)
Check (($faults | ConvertTo-Json) -notmatch 'PRIVATE_SENTINEL|SYNTHETIC|\.cs|\.cpp|[\\/]')
foreach($where in @('Program.cs(0)','Program.cs(100001)','NotProgram.cs(12)','Program.cs.backup(12)')){$row=@(Get-LifecycleBuildEvidence ($where+': error CS4014: '+$secret))[0];Check ($row.Source -ceq 'Unknown' -and $null -eq $row.Line)}
Check (@(Get-LifecycleBuildEvidence ('x'*4194305)).Count -eq 0)
Check (@(Get-LifecycleBuildEvidence (('x'*8200)+': error CS4014: private')).Count -eq 0)
Check (@(Get-LifecycleBuildEvidence ((1..50 | ForEach-Object {"Program.cs($_): error CS4014: $secret"}) -join "`n")).Count -eq 16)
# Compile only. No Windows P/Invoke or process launch on this Linux check.
Add-Type -Path "$PSScriptRoot/../../tools/ci/LifecycleOperation.cs"
Check ([KuaiYiLifecycleOperation]::Quote('plain') -ceq '"plain"')
Check ([KuaiYiLifecycleOperation]::Quote('C:\folder\') -ceq '"C:\folder\\"')
Check ([KuaiYiLifecycleOperation]::Quote('a"b') -ceq '"a\"b"')
foreach($pair in @(@('StartupInfo',104),@('ProcessInfo',24),@('ExtendedLimits',144),@('Accounting',48),@('SecurityAttributes',24))){$type=[KuaiYiLifecycleOperation].GetNestedType($pair[0],[Reflection.BindingFlags]::NonPublic);$sizeMethod=@([Runtime.InteropServices.Marshal].GetMethods() | Where-Object {$_.Name -ceq 'SizeOf' -and $_.IsGenericMethodDefinition -and $_.GetParameters().Count -eq 0})[0];Check ($sizeMethod.MakeGenericMethod($type).Invoke($null,@()) -eq $pair[1])}
$script=Get-Content -LiteralPath "$PSScriptRoot/run-lifecycle-windows-ci.ps1" -Raw
Check ($script.Contains("'LifecycleRuntime' "+'$dotnet @($dll,'+"'--supervise',"+'$exe)'))
Check ($script -notmatch 'Install-|Get-CiEnvironment|WindowsFacts|upload-artifact|KuaiYi\.ReuseLifecycle\.exe')
if(-not $IsWindows){$output=@(& (Join-Path $PSHOME 'pwsh') -NoProfile -File "$PSScriptRoot/run-lifecycle-windows-ci.ps1" *>&1);Check ($LASTEXITCODE -eq 2 -and $output.Count -eq 1);$summary=([string]$output[0]).Substring('KUAIYI_LIFECYCLE_CI_SUMMARY '.Length) | ConvertFrom-Json;Check ($summary.Status -ceq 'Blocked' -and $summary.Runtime -ceq 'NotRun' -and $summary.Operations.Count -eq 0)}
Write-Host "LIFECYCLE_CI_EVIDENCE_CONTRACTS passed=$checks failed=0; Windows operation wrapper NotRun"
