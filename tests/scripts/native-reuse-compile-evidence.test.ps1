Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/../../scripts/sdk-compile-evidence.ps1"
. "$PSScriptRoot/native-reuse-source-gate.ps1"
$checks=0
function Check([bool]$Condition){if(-not $Condition){throw ('Native compile evidence contract failed at check '+$script:checks)};$script:checks++}
$secret='PRIVATE_SENTINEL_SYNTHETIC_sk'
foreach($pair in @(@('NativeWorker.cpp','NativeWorker'),@('SelectionCapture.h','SelectionCapture'),@('RequestLifetime.h','RequestLifetime'),@('NativeProtocol.h','NativeProtocol'))){
    $rows=@(Get-SdkCompilerEvidence "C:\$secret\repo\$($pair[0])(67,8): error C2440: $secret" -Profile NativeReuse)
    Check ($rows.Count -eq 1 -and $rows[0].Source -ceq $pair[1] -and $rows[0].Code -ceq 'C2440' -and $rows[0].Line -eq 67)
    Check ((@($rows[0].PSObject.Properties.Name) -join ',') -ceq 'Code,Source,Line')
    Check (($rows | ConvertTo-Json) -notmatch 'PRIVATE_SENTINEL|SYNTHETIC|\.h|\.cpp|[\\/]')
}
foreach($location in @('UIAutomation.h(55)','wrl/client.h(55)','ComCaptureProbe.cpp(55)','Unknown.h(55)','NotNativeWorker.cpp(55)','NativeWorker.cpp.backup(55)','NativeWorker.cpp(0)','NativeWorker.cpp(100001)','NativeWorker.cpp(-1)','NativeWorker.cpp(9,PRIVATE_SENTINEL)')){
    $row=@(Get-SdkCompilerEvidence ($location+': error C2440: '+$secret) -Profile NativeReuse)[0]
    Check ($row.Source -ceq 'Unknown' -and $null -eq $row.Line)
}
foreach($position in @(1,100000)){Check (@(Get-SdkCompilerEvidence "NativeWorker.cpp($position): warning C4267: $secret" -Profile NativeReuse)[0].Line -eq $position)}
Check (@(Get-SdkCompilerEvidence "NativeWorker.cpp(44): error C9999: $secret" -Profile NativeReuse)[0].Code -ceq 'Unknown')
Check (@(Get-SdkCompilerEvidence "private.obj : error LNK2019: $secret" -Profile NativeReuse)[0].Source -ceq 'Unknown')
Check (@(Get-SdkCompilerEvidence "private.cpp(8): error C2440: NativeWorker.cpp(99): error C2664: $secret" -Profile NativeReuse)[0].Source -ceq 'Unknown')
Check (@(Get-SdkCompilerEvidence ($secret+('x'*8200)+': error C2440: private') -Profile NativeReuse).Count -eq 0)
Check (@(Get-SdkCompilerEvidence ('x'*4194305) -Profile NativeReuse).Count -eq 0)
Check (@(Get-SdkCompilerEvidence ((1..50 | ForEach-Object {"NativeWorker.cpp($_): error C2440: $secret"}) -join "`n") -Profile NativeReuse).Count -eq 16)
Check (@(Get-SdkCompilerEvidence "NativeWorker.cpp(44): error C2440: $secret`nNativeWorker.cpp(44): error C2440: $secret" -Profile NativeReuse).Count -eq 1)
Check (@(Get-SdkCompilerEvidence "$secret arbitrary untranslated original" -Profile NativeReuse).Count -eq 0)
$runner=Get-Content -LiteralPath "$PSScriptRoot/build-native-reuse.ps1" -Raw
Check ($runner -match '-Profile NativeReuse' -and $runner -match 'ConfigureReleaseX64LinkOnly')
Check ($runner -notmatch 'Get-CiEnvironment|WindowsFacts|Install-|Tee-Object|Write-Output.*log|Get-Content.*Write-Host')
Check ($runner -match 'cmake -S.*\*> \$configure' -and $runner -match 'cmake --build.*\*> \$build')
# Linux executes only the preflight Blocked path; never instantiates Windows APIs or runs a worker.
if(-not $IsWindows){
    $visible=@(& (Join-Path $PSHOME 'pwsh') -NoProfile -File "$PSScriptRoot/build-native-reuse.ps1" *>&1)
    Check ($LASTEXITCODE -eq 2 -and $visible.Count -eq 1)
    $line=[string]$visible[0];Check ($line.StartsWith('KUAIYI_NATIVE_REUSE_COMPILE_SUMMARY '))
    $summary=$line.Substring('KUAIYI_NATIVE_REUSE_COMPILE_SUMMARY '.Length) | ConvertFrom-Json
    Check ($summary.Status -ceq 'Blocked' -and $summary.ConfigureStatus -ceq 'NotRun' -and $summary.BuildStatus -ceq 'NotRun' -and -not $summary.ExecutableExists)
    Check ($summary.Worker -ceq 'NotRun' -and $summary.Uia -ceq 'NotRun' -and $summary.Com -ceq 'NotRun' -and $summary.Job -ceq 'NotRun' -and $summary.Match -ceq 'NotRun' -and $summary.ModelCalls -eq 0)
}
Write-Host "NATIVE_REUSE_COMPILE_EVIDENCE_CONTRACTS passed=$checks failed=0"
function git {
    $script:GitCalls++
    if($script:GitCalls -eq 1){$global:LASTEXITCODE=$script:HeadExit;if($script:HasHead){$script:Head}}
    else {$global:LASTEXITCODE=$script:StatusExit;if($script:Dirty){'M private-source'}}
}
$sourceChecks=0
$script:Head='a'*40
foreach($case in @(@(0,0,$true,$false,$true),@(0,1,$true,$false,$false),@(1,0,$true,$false,$false),@(0,0,$true,$true,$false),@(0,0,$false,$false,$false))){
    $script:GitCalls=0;$script:HeadExit=$case[0];$script:StatusExit=$case[1];$script:HasHead=$case[2];$script:Dirty=$case[3]
    $accepted=$false
    try{$result=Get-NativeReuseSourceCommit 'synthetic-repository' $script:Head;$accepted=$result -ceq $script:Head}catch{}
    if($accepted -ne $case[4]){throw 'Source git exit contract failed'};$sourceChecks++
}
$script:GitCalls=0;$script:HeadExit=0;$script:StatusExit=0;$script:HasHead=$true;$script:Dirty=$false
$accepted=$false;try{[void](Get-NativeReuseSourceCommit 'synthetic-repository' ('b'*40));$accepted=$true}catch{}
if($accepted){throw 'Source identity contract failed'};$sourceChecks++
Remove-Item Function:git
Write-Host "NATIVE_REUSE_SOURCE_GATE_CONTRACTS passed=$sourceChecks failed=0"
